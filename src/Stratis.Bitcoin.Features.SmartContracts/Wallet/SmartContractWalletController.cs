﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.MemoryPool.Broadcasting;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using State = Stratis.Bitcoin.Features.MemoryPool.Broadcasting.State;

namespace Stratis.Bitcoin.Features.SmartContracts.Wallet
{
    [ApiVersion("1")]
    [Route("api/[controller]")]
    public sealed class SmartContractWalletController : Controller
    {
        private readonly IBroadcasterManager broadcasterManager;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly IConnectionManager connectionManager;
        private readonly ILogger logger;
        private readonly Network network;
        private readonly IReceiptRepository receiptRepository;
        private readonly IWalletManager walletManager;
        private readonly ISmartContractTransactionService smartContractTransactionService;

        public SmartContractWalletController(
            IBroadcasterManager broadcasterManager,
            ICallDataSerializer callDataSerializer,
            IConnectionManager connectionManager,
            ILoggerFactory loggerFactory,
            Network network,
            IReceiptRepository receiptRepository,
            IWalletManager walletManager,
            ISmartContractTransactionService smartContractTransactionService)
        {
            this.broadcasterManager = broadcasterManager;
            this.callDataSerializer = callDataSerializer;
            this.connectionManager = connectionManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.receiptRepository = receiptRepository;
            this.walletManager = walletManager;
            this.smartContractTransactionService = smartContractTransactionService;
        }

        private IEnumerable<HdAddress> GetAccountAddressesWithBalance(string walletName)
        {
            return this.walletManager
                .GetSpendableTransactionsInWallet(walletName)
                .GroupBy(x => x.Address)
                .Where(grouping => grouping.Sum(x => x.Transaction.GetUnspentAmount(true)) > 0)
                .Select(grouping => grouping.Key);
        }

        /// <summary>
        /// Gets the balance at a specific wallet address in STRAT (or the sidechain coin).
        /// This method gets the UTXOs at the address that the wallet can spend.
        /// The function can be used to query the balance at a smart contract account address
        /// supplied by /api/SmartContractWallet/account-addresses.
        /// </summary>
        ///
        /// <param name="walletName">The address at which to retrieve the balance.</param>
        /// 
        /// <returns>The balance at a specific wallet address in STRAT (or the sidechain coin).</returns>
        [Route("address-balance")]
        [HttpGet]
        public IActionResult GetAddressBalance(string address)
        {
            AddressBalance balance = this.walletManager.GetAddressBalance(address);

            return this.Json(balance.AmountConfirmed.ToUnit(MoneyUnit.Satoshi));
        }

        /// <summary>
        /// Gets the history of a specific wallet address.
        /// This includes the smart contract create and call transactions
        /// This method can be used to query the balance at a smart contract account address
        /// supplied by /api/SmartContractWallet/account-addresses. Indeed,
        /// it is advisable to use /api/SmartContractWallet/account-addresses
        /// to generate an address for all smart contract interactions.
        /// If this has been done, and that address is supplied to this method,
        /// a list of all smart contract interactions for a wallet will be returned.
        /// </summary>
        ///
        /// <param name="walletName">The name of the wallet holding the address.</param>
        /// <param name="address">The address to retrieve the history for.</param>
        /// <returns>A list of smart contract create and call transaction items as well as transaction items at a specific wallet address.</returns>
        [Route("history")]
        [HttpGet]
        public IActionResult GetHistory(GetHistoryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WalletName))
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "No wallet name", "No wallet name provided");

            if (string.IsNullOrWhiteSpace(request.Address))
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "No address", "No address provided");

            try
            {
                var transactionItems = new List<ContractTransactionItem>();

                HdAccount account = this.walletManager.GetAccounts(request.WalletName).First();

                // Get a list of all the transactions found in an account (or in a wallet if no account is specified), with the addresses associated with them.
                IEnumerable<AccountHistory> accountsHistory = this.walletManager.GetHistory(request.WalletName, account.Name);

                // Wallet manager returns only 1 when an account name is specified.
                AccountHistory accountHistory = accountsHistory.First();

                List<FlatHistory> items = accountHistory.History.Where(x => x.Address.Address == request.Address).ToList();

                // Represents a sublist of transactions associated with receive addresses + a sublist of already spent transactions associated with change addresses.
                // In effect, we filter out 'change' transactions that are not spent, as we don't want to show these in the history.
                List<FlatHistory> history = items.Where(t => !t.Address.IsChangeAddress() || (t.Address.IsChangeAddress() && t.Transaction.IsSpent())).ToList();

                // TransactionData in history is confusingly named. A "TransactionData" actually represents an input, and the outputs that spend it are "SpendingDetails".
                // There can be multiple "TransactionData" which have the same "SpendingDetails".
                // For SCs we need to group spending details by their transaction ID, to get all the inputs related to the same outputs.
                // Each group represents 1 SC transaction.
                // Each item.Transaction in a group is an input.
                // Each item.Transaction.SpendingDetails in the group represent the outputs, and they should all be the same so we can pick any one.
                var scTransactions = history
                    .Where(item => item.Transaction.SpendingDetails != null)
                    .Where(item => item.Transaction.SpendingDetails.Payments.Any(x => x.DestinationScriptPubKey.IsSmartContractExec()))
                    .GroupBy(item => item.Transaction.SpendingDetails.TransactionId)
                    .Skip(request.Skip ?? 0)
                    .Take(request.Take ?? history.Count)
                    .Select(g => new
                    {
                        TransactionId = g.Key,
                        InputAmount = g.Sum(i => i.Transaction.Amount), // Sum the inputs to the SC transaction.
                        Outputs = g.First().Transaction.SpendingDetails.Payments, // Each item in the group will have the same outputs.
                        OutputAmount = g.First().Transaction.SpendingDetails.Payments.Sum(o => o.Amount),
                        BlockHeight = g.First().Transaction.SpendingDetails.BlockHeight // Each item in the group will have the same block height.
                    })
                    .ToList();

                // Get all receipts in one transaction
                IList<Receipt> receipts = this.receiptRepository.RetrieveMany(scTransactions.Select(x => x.TransactionId).ToList());

                for (int i = 0; i < scTransactions.Count; i++)
                {
                    var scTransaction = scTransactions[i];
                    Receipt receipt = receipts[i];

                    // Consensus rules state that each transaction can have only one smart contract exec output.
                    PaymentDetails scPayment = scTransaction.Outputs.First(x => x.DestinationScriptPubKey.IsSmartContractExec());

                    // This will always give us a value - the transaction has to be serializable to get past consensus.
                    Result<ContractTxData> txDataResult = this.callDataSerializer.Deserialize(scPayment.DestinationScriptPubKey.ToBytes());
                    ContractTxData txData = txDataResult.Value;

                    // If the receipt is not available yet, we don't know how much gas was consumed so use the full gas budget.
                    ulong gasFee = receipt != null
                        ? receipt.GasUsed * receipt.GasPrice
                        : txData.GasCostBudget;

                    long totalFees = scTransaction.InputAmount - scTransaction.OutputAmount;
                    Money transactionFee = Money.FromUnit(totalFees, MoneyUnit.Satoshi) - Money.FromUnit(txData.GasCostBudget, MoneyUnit.Satoshi);

                    var result = new ContractTransactionItem
                    {
                        Amount = scPayment.Amount.ToUnit(MoneyUnit.Satoshi),
                        BlockHeight = scTransaction.BlockHeight,
                        Hash = scTransaction.TransactionId,
                        TransactionFee = transactionFee.ToUnit(MoneyUnit.Satoshi),
                        GasFee = gasFee
                    };

                    if (scPayment.DestinationScriptPubKey.IsSmartContractCreate())
                    {
                        result.Type = ContractTransactionItemType.ContractCreate;
                        result.To = receipt?.NewContractAddress?.ToBase58Address(this.network) ?? string.Empty;
                    }
                    else if (scPayment.DestinationScriptPubKey.IsSmartContractCall())
                    {
                        result.Type = ContractTransactionItemType.ContractCall;
                        result.To = txData.ContractAddress.ToBase58Address(this.network);
                    }

                    transactionItems.Add(result);
                }

                return this.Json(transactionItems.OrderByDescending(x => x.BlockHeight ?? Int32.MaxValue));
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Builds a transaction to create a smart contract and then broadcasts the transaction to the network.
        /// If the deployment is successful, methods on the smart contract can be subsequently called.
        /// </summary>
        /// 
        /// <param name="request">An object containing the necessary parameters to build the transaction.</param>
        /// 
        /// <returns>A hash of the transaction used to create the smart contract. The result of the transaction broadcast is not returned,
        /// and you should check for a transaction receipt to see if it was successful.</returns>
        [Route("create")]
        [HttpPost]
        public IActionResult Create([FromBody] BuildCreateContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            BuildCreateContractTransactionResponse response = this.smartContractTransactionService.BuildCreateTx(request);

            if (!response.Success)
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, response.Message, string.Empty);

            Transaction transaction = this.network.CreateTransaction(response.Hex);

            this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

            // Check if transaction was actually added to a mempool.
            TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(transaction.GetHash());

            if (transactionBroadCastEntry?.State == State.CantBroadcast)
            {
                this.logger.LogError("Exception occurred: {0}", transactionBroadCastEntry.ErrorMessage);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, transactionBroadCastEntry.ErrorMessage, "Transaction Exception");
            }

            return this.Json(response.TransactionId);
        }

        /// <summary>
        /// Builds a transaction to call a smart contract method and then broadcasts the transaction to the network.
        /// If the call is successful, any changes to the smart contract balance or persistent data are propagated
        /// across the network.
        /// </summary>
        /// 
        /// <param name="request">An object containing the necessary parameters to build the transaction.</param>
        ///
        /// <returns>The transaction used to call a smart contract method. The result of the transaction broadcast is not returned,
        /// and you should check for a transaction receipt to see if it was successful.</returns>
        [Route("call")]
        [HttpPost]
        public IActionResult Call([FromBody] BuildCallContractTransactionRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            BuildCallContractTransactionResponse response = this.smartContractTransactionService.BuildCallTx(request);

            if (!response.Success)
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, response.Message,string.Empty);

            Transaction transaction = this.network.CreateTransaction(response.Hex);

            this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

            // Check if transaction was actually added to a mempool.
            TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(transaction.GetHash());

            if (transactionBroadCastEntry?.State == State.CantBroadcast)
            {
                this.logger.LogError("Exception occurred: {0}", transactionBroadCastEntry.ErrorMessage);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, transactionBroadCastEntry.ErrorMessage, "Transaction Exception");
            }

            return this.Json(response);
        }

        /// <summary>
        /// Broadcasts a transaction, which either creates a smart contract or calls a method on a smart contract.
        /// If the contract deployment or method call are successful gas and fees are consumed.
        /// </summary>
        /// 
        /// <param name="request">An object containing the necessary parameters to send the transaction.</param>
        /// 
        /// <returns>A model of the transaction which the Broadcast Manager broadcasts. The result of the transaction broadcast is not returned,
        /// and you should check for a transaction receipt to see if it was successful.</returns>
        [Route("send-transaction")]
        [HttpPost]
        public IActionResult SendTransaction([FromBody] SendTransactionRequest request)
        {
            Guard.NotNull(request, nameof(request));

            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            if (!this.connectionManager.ConnectedPeers.Any())
                throw new WalletException("Can't send transaction: sending transaction requires at least one connection!");

            try
            {
                Transaction transaction = this.network.CreateTransaction(request.Hex);

                var model = new SendTransactionModel
                {
                    TransactionId = transaction.GetHash(),
                    Outputs = new List<TransactionOutputModel>()
                };

                foreach (TxOut output in transaction.Outputs)
                {
                    bool isUnspendable = output.ScriptPubKey.IsUnspendable;

                    string address = this.GetAddressFromScriptPubKey(output);
                    model.Outputs.Add(new TransactionOutputModel
                    {
                        Address = address,
                        Amount = output.Value,
                        OpReturnData = isUnspendable ? Encoding.UTF8.GetString(output.ScriptPubKey.ToOps().Last().PushData) : null
                    });
                }

                this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

                TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(transaction.GetHash());
                if (!string.IsNullOrEmpty(transactionBroadCastEntry?.ErrorMessage))
                {
                    this.logger.LogError("Exception occurred: {0}", transactionBroadCastEntry.ErrorMessage);
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, transactionBroadCastEntry.ErrorMessage, "Transaction Exception");
                }

                return this.Json(model);
            }
            catch (Exception e)
            {
                this.logger.LogError("Exception occurred: {0}", e.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
        }

        /// <summary>
        /// Retrieves a string that represents the receiving address for an output.For smart contract transactions,
        /// returns the opcode that was sent i.e.OP_CALL or OP_CREATE
        /// </summary>
        private string GetAddressFromScriptPubKey(TxOut output)
        {
            if (output.ScriptPubKey.IsSmartContractExec())
                return output.ScriptPubKey.ToOps().First().Code.ToString();

            if (!output.ScriptPubKey.IsUnspendable)
                return output.ScriptPubKey.GetDestinationAddress(this.network).ToString();

            return null;
        }
    }
}
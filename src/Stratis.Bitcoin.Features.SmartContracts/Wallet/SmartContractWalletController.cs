using System;
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
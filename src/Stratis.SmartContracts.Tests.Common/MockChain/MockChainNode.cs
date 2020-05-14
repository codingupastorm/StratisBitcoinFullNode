using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Models;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Features.SmartContracts;
using Stratis.Features.SmartContracts.Models;
using Stratis.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Features.SmartContracts.ReflectionExecutor.Controllers;
using Stratis.Features.Wallet;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Local;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Tests.Common.MockChain
{
    /// <summary>
    /// Facade for CoreNode.
    /// </summary>
    public class MockChainNode
    {
        public readonly string WalletName = "mywallet";
        public readonly string Password = "password";
        public readonly string Passphrase = "passphrase";
        public readonly string AccountName = "account 0";

        // Services on the node. Used to retrieve information about the state of the network.
        private readonly SmartContractsController smartContractsController;
        //private readonly SmartContractWalletController smartContractWalletController;
        private readonly IStateRepositoryRoot stateRoot;
        private readonly IBlockStore blockStore;

        /// <summary>
        /// The chain / network this node is part of.
        /// </summary>
        private readonly IMockChain chain;

        /// <summary>
        /// Reference to the complex underlying node object.
        /// </summary>
        public CoreNode CoreNode { get; }

        /// <summary>
        /// The address that all new coins are mined to.
        /// </summary>
        public HdAddress MinerAddress { get; }

        /// <summary>
        /// The transactions available to be spent from this node's wallet.
        /// </summary>
        public IEnumerable<UnspentOutputReference> SpendableTransactions
        {
            get
            {
                return this.CoreNode.FullNode.WalletManager().GetSpendableTransactionsInWallet(this.WalletName);
            }
        }

        /// <summary>
        /// The balance currently available to be spent by this node's wallet.
        /// </summary>
        public Money WalletSpendableBalance
        {
            get
            {
                return this.SpendableTransactions.Sum(s => s.Transaction.Amount);
            }
        }

        public MockChainNode(CoreNode coreNode, IMockChain chain, Mnemonic mnemonic = null)
        {
            this.CoreNode = coreNode;
            this.chain = chain;

            // Set up address and mining
            (Wallet wallet, _) = this.CoreNode.FullNode.WalletManager().CreateWallet(this.Password, this.WalletName, this.Passphrase, mnemonic);
            HdAccount account = wallet.GetAccount(this.AccountName);
            this.MinerAddress = account.GetFirstUnusedReceivingAddress();

            Key key = wallet.GetExtendedPrivateKeyForAddress(this.Password, this.MinerAddress).PrivateKey;
            this.CoreNode.SetMinerSecret(new BitcoinSecret(key, this.CoreNode.FullNode.Network));

            // Set up services for later
            //this.smartContractWalletController = this.CoreNode.FullNode.NodeController<SmartContractWalletController>();
            this.smartContractsController = this.CoreNode.FullNode.NodeController<SmartContractsController>();
            this.stateRoot = this.CoreNode.FullNode.NodeService<IStateRepositoryRoot>();
            this.blockStore = this.CoreNode.FullNode.NodeService<IBlockStore>();
        }

        /// <summary>
        /// Mine the given number of blocks. The block reward will go to this node's MinerAddress.
        /// </summary>
        public void MineBlocks(int amountOfBlocks)
        {
            TestHelper.MineBlocks(this.CoreNode, amountOfBlocks);
            this.chain.WaitForAllNodesToSync();
        }

        /// <summary>
        /// Get an unused address that can be used to send funds to this node.
        /// </summary>
        public HdAddress GetUnusedAddress()
        {
            return this.CoreNode.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(this.WalletName, this.AccountName));
        }

        /// <summary>
        /// Sends a create contract transaction. Note that before this transaction can be mined it will need to reach the mempool.
        /// You will likely want to call 'WaitMempoolCount' after this.
        /// </summary>
        public BuildCreateContractTransactionResponse SendCreateContractTransaction(
            byte[] contractCode,
            decimal amount,
            string[] parameters = null,
            ulong gasLimit = SmartContractFormatLogic.GasLimitMaximum / 2, // half of maximum
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice,
            decimal feeAmount = 0.01M,
            string sender = null,
            List<OutpointRequest> outpoints = null)
        {
            var request = new BuildCreateContractTransactionRequest
            {
                Amount = amount.ToString(CultureInfo.InvariantCulture),
                AccountName = this.AccountName,
                ContractCode = contractCode.ToHexString(),
                FeeAmount = feeAmount.ToString(CultureInfo.InvariantCulture),
                GasLimit = gasLimit,
                GasPrice = gasPrice,
                Parameters = parameters,
                Password = this.Password,
                Sender = sender ?? this.MinerAddress.Address,
                WalletName = this.WalletName,
                Outpoints = outpoints
            };

            IActionResult result = this.smartContractsController.BuildAndSendCreateSmartContractTransactionAsync(request).GetAwaiter().GetResult();
            if (result is JsonResult response)
            {
                return (BuildCreateContractTransactionResponse)response.Value;
            }

            return null;
        }

        /// <summary>
        /// Retrieves receipts for all cases where a specific event was logged in a specific contract.
        /// </summary>
        public IList<ReceiptResponse> GetReceipts(string contractAddress, string eventName)
        {
            JsonResult response = (JsonResult)this.smartContractsController.ReceiptSearch(contractAddress, eventName).Result;
            return (IList<ReceiptResponse>)response.Value;
        }

        public ReceiptResponse GetReceipt(string txHash)
        {
            JsonResult response = (JsonResult)this.smartContractsController.GetReceipt(txHash);
            return (ReceiptResponse)response.Value;
        }

        /// <summary>
        /// Sends a call contract transaction. Note that before this transaction can be mined it will need to reach the mempool.
        /// You will likely want to call 'WaitMempoolCount' after this.
        /// </summary>
        public BuildCallContractTransactionResponse SendCallContractTransaction(
            string methodName,
            string contractAddress,
            decimal amount,
            string[] parameters = null,
            ulong gasLimit = SmartContractFormatLogic.GasLimitMaximum / 2, // half of maximum
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice,
            decimal feeAmount = 0.01M,
            string sender = null,
            List<OutpointRequest> outpoints = null)
        {
            var request = new BuildCallContractTransactionRequest
            {
                AccountName = this.AccountName,
                Amount = amount.ToString(CultureInfo.InvariantCulture),
                ContractAddress = contractAddress,
                FeeAmount = feeAmount.ToString(CultureInfo.InvariantCulture),
                GasLimit = gasLimit,
                GasPrice = gasPrice,
                MethodName = methodName,
                Parameters = parameters,
                Password = this.Password,
                Sender = sender ?? this.MinerAddress.Address,
                WalletName = this.WalletName,
                Outpoints = outpoints
            };

            var response = (JsonResult)this.smartContractsController.BuildAndSendCallSmartContractTransactionAsync(request).GetAwaiter().GetResult();

            return (BuildCallContractTransactionResponse)response.Value;
        }

        public ILocalExecutionResult CallContractMethodLocally(
            string methodName,
            string contractAddress,
            decimal amount,
            string[] parameters = null,
            ulong gasLimit = SmartContractFormatLogic.GasLimitMaximum / 2, // half of maximum
            ulong gasPrice = SmartContractMempoolValidator.MinGasPrice,
            string sender = null)
        {
            var request = new LocalCallContractRequest
            {
                Amount = amount.ToString(CultureInfo.InvariantCulture),
                ContractAddress = contractAddress,
                MethodName = methodName,
                Parameters = parameters,
                Sender = sender ?? this.MinerAddress.Address
            };
            JsonResult response = (JsonResult)this.smartContractsController.LocalCallSmartContractTransaction(request);
            return (ILocalExecutionResult)response.Value;
        }

        /// <summary>
        /// Get the balance of a particular contract address.
        /// </summary>
        public ulong GetContractBalance(string contractAddress)
        {
            return this.stateRoot.GetCurrentBalance(contractAddress.ToUint160(this.CoreNode.FullNode.Network));
        }

        /// <summary>
        /// Get the bytecode stored at a particular contract address.
        /// </summary>
        public byte[] GetCode(string contractAddress)
        {
            return this.stateRoot.GetCode(contractAddress.ToUint160(this.CoreNode.FullNode.Network));
        }

        /// <summary>
        /// Get the bytes stored at a particular key in a particular address.
        /// </summary>
        public byte[] GetStorageValue(string contractAddress, string key)
        {
            return this.stateRoot.GetStorageValue(contractAddress.ToUint160(this.CoreNode.FullNode.Network), Encoding.UTF8.GetBytes(key)).Value;
        }

        /// <summary>
        /// Get the last block mined. AKA the current tip.
        /// </summary
        public NBitcoin.Block GetLastBlock()
        {
            return this.blockStore.GetBlock(this.CoreNode.FullNode.ChainIndexer.Tip.HashBlock);
        }

        /// <summary>
        /// Wait until the amount of transactions in the mempool reaches the given number.
        /// </summary>
        public void WaitMempoolCount(int num)
        {
            TestBase.WaitLoop(() => this.CoreNode.FullNode.MempoolManager().GetMempoolAsync().GetAwaiter().GetResult().Count >= num);
        }
    }
}

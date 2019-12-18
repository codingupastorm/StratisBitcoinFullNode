using System;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.RuntimeObserver;
using State = Stratis.Bitcoin.Features.Wallet.Broadcasting.State;

namespace Stratis.Feature.PoA.Tokenless.Controllers
{
    [ApiVersion("1")]
    [Route("api/[controller]")]
    public class TokenlessController : Controller
    {
        private readonly Network network;
        private readonly ITokenlessSigner tokenlessSigner;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly IAddressGenerator addressGenerator;
        private readonly IConnectionManager connectionManager;
        private readonly IBroadcasterManager broadcasterManager;
        private readonly ILogger logger;

        public TokenlessController(Network network,
            ITokenlessSigner tokenlessSigner,
            ICallDataSerializer callDataSerializer,
            IAddressGenerator addressGenerator,
            IConnectionManager connectionManager,
            IBroadcasterManager broadcasterManager,
            ILoggerFactory loggerFactory)
        {
            this.network = network;
            this.tokenlessSigner = tokenlessSigner;
            this.callDataSerializer = callDataSerializer;
            this.addressGenerator = addressGenerator;
            this.connectionManager = connectionManager;
            this.broadcasterManager = broadcasterManager;
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        // TODO: This might be slightly ridiculous, that it passes the mnemonic in. This is just to get it to a testable state for now.

        [Route("tokenless-data")]
        public IActionResult BuildOpReturnTransaction(string mnemonic, byte[] data)
        {
            Transaction transaction = this.network.CreateTransaction();
            Script outputScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(data);
            transaction.Outputs.Add(new TxOut(Money.Zero, outputScript));

            Key key = new Mnemonic(mnemonic).DeriveExtKey().PrivateKey;

            this.tokenlessSigner.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return Json(new WalletBuildTransactionModel
            {
                Hex = transaction.ToHex(),
                TransactionId = transaction.GetHash()
            });
        }

        // TODO: Method params

        // TODO: Some error handling? Have not tested any failure cases at all.

        // TODO: Was this the plan to create a new controller or should this be happening in a replacement Wallet ? etc.

        [Route("tokenless-create")]
        public IActionResult BuildCreateContractTransaction(string mnemonic, byte[] contractCode)
        {
            Transaction transaction = this.network.CreateTransaction();

            var contractTxData = new ContractTxData(0, 0, (Gas)0, contractCode);
            byte[] outputScript = this.callDataSerializer.Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            Key key = new Mnemonic(mnemonic).DeriveExtKey().PrivateKey;

            this.tokenlessSigner.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return Json(
                BuildCreateContractTransactionResponse.Succeeded(
                    transaction, 
                    0, 
                    this.addressGenerator.GenerateAddress(transaction.GetHash(), 0).ToBase58Address(this.network)));
        }

        [Route("tokenless-call")]
        public IActionResult BuildCallContractTransaction(string mnemonic, string address, string method)
        {
            Transaction transaction = this.network.CreateTransaction();

            var contractTxData = new ContractTxData(0, 0, (Gas)0, address.ToUint160(this.network), method);
            byte[] outputScript = this.callDataSerializer.Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            Key key = new Mnemonic(mnemonic).DeriveExtKey().PrivateKey;

            this.tokenlessSigner.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return Json(BuildCallContractTransactionResponse.Succeeded(method, transaction, 0));
        }

        [Route("tokenless-send")]
        [HttpPost]
        public IActionResult SendTransaction([FromBody] string hex)
        {

            if (!this.connectionManager.ConnectedPeers.Any())
            {
                this.logger.LogTrace("(-)[NO_CONNECTED_PEERS]");
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Forbidden, "Can't send transaction: sending transaction requires at least one connection!", string.Empty);
            }

            try
            {
                Transaction transaction = this.network.CreateTransaction(hex);

                var model = new WalletSendTransactionModel
                {
                    TransactionId = transaction.GetHash()
                };

                // TODO: Show some outputs or something?

                this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

                TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(transaction.GetHash());

                if (transactionBroadCastEntry.State == State.CantBroadcast)
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
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Decompilation;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
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
        private readonly IMethodParameterStringSerializer methodParameterSerializer;
        private readonly IBlockStore blockStore;
        private readonly ChainIndexer chainIndexer;
        private readonly IStateRepositoryRoot stateRoot;
        private readonly IReceiptRepository receiptRepository;
        private readonly CSharpContractDecompiler contractDecompiler;
        private readonly IContractPrimitiveSerializer primitiveSerializer;
        private readonly ISerializer serializer;
        private readonly ILogger logger;

        public TokenlessController(Network network,
            ITokenlessSigner tokenlessSigner,
            ICallDataSerializer callDataSerializer,
            IAddressGenerator addressGenerator,
            IConnectionManager connectionManager,
            IBroadcasterManager broadcasterManager,
            IMethodParameterStringSerializer methodParameterSerializer,
            IBlockStore blockStore,
            ChainIndexer chainIndexer,
            IStateRepositoryRoot stateRoot,
            IReceiptRepository receiptRepository,
            CSharpContractDecompiler contractDecompiler,
            IContractPrimitiveSerializer primitiveSerializer,
            ISerializer serializer,
            ILoggerFactory loggerFactory)
        {
            this.network = network;
            this.tokenlessSigner = tokenlessSigner;
            this.callDataSerializer = callDataSerializer;
            this.addressGenerator = addressGenerator;
            this.connectionManager = connectionManager;
            this.broadcasterManager = broadcasterManager;
            this.methodParameterSerializer = methodParameterSerializer;
            this.blockStore = blockStore;
            this.chainIndexer = chainIndexer;
            this.stateRoot = stateRoot;
            this.receiptRepository = receiptRepository;
            this.contractDecompiler = contractDecompiler;
            this.primitiveSerializer = primitiveSerializer;
            this.serializer = serializer;
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        // TODO: This might be slightly ridiculous, that it passes the mnemonic in. This is just to get it to a testable state for now.

        [Route("data")]
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

        // TODO: Some error handling? Have not tested any failure cases at all.

        // TODO: Was this the plan to create a new controller or should this be happening in a replacement Wallet ? etc.

        [Route("create")]
        public IActionResult BuildCreateContractTransaction(string mnemonic, byte[] contractCode, string[] parameters = null)
        {
            object[] methodParameters = null;

            if (parameters != null && parameters.Length > 0)
            {
                try
                {
                    methodParameters = this.methodParameterSerializer.Deserialize(parameters);
                }
                catch (MethodParameterStringSerializerException exception)
                {
                    return Json(BuildCreateContractTransactionResponse.Failed(exception.Message));
                }
            }

            Transaction transaction = this.network.CreateTransaction();

            var contractTxData = new ContractTxData(0, 0, (Gas)0, contractCode, methodParameters);
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

        [Route("call")]
        public IActionResult BuildCallContractTransaction(string mnemonic, string address, string method, string[] parameters = null)
        {
            object[] methodParameters = null;

            if (parameters != null && parameters.Length > 0)
            {
                try
                {
                    methodParameters = this.methodParameterSerializer.Deserialize(parameters);
                }
                catch (MethodParameterStringSerializerException exception)
                {
                    return Json(BuildCreateContractTransactionResponse.Failed(exception.Message));
                }
            }

            Transaction transaction = this.network.CreateTransaction();

            var contractTxData = new ContractTxData(0, 0, (Gas)0, address.ToUint160(this.network), method, methodParameters);
            byte[] outputScript = this.callDataSerializer.Serialize(contractTxData);
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(outputScript)));

            Key key = new Mnemonic(mnemonic).DeriveExtKey().PrivateKey;

            this.tokenlessSigner.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.network));

            return Json(BuildCallContractTransactionResponse.Succeeded(method, transaction, 0));
        }

        [Route("send")]
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

        /// <summary>
        /// Gets a single piece of smart contract data, which was stored as a key–value pair using the
        /// SmartContract.PersistentState property. 
        /// The method performs a lookup in the smart contract
        /// state database for the supplied smart contract address and key.
        /// The value associated with the given key, deserialized for the specified data type, is returned.
        /// If the key does not exist or deserialization fails, the method returns the default value for
        /// the specified type.
        /// </summary>
        /// 
        /// <param name="request">An object containing the necessary parameters to perform a retrieve stored data request.</param>
        ///
        /// <returns>A single piece of stored smart contract data.</returns>
        [Route("storage")]
        [HttpGet]
        public IActionResult GetStorage([FromQuery] GetStorageRequest request)
        {
            if (!this.ModelState.IsValid)
            {
                this.logger.LogTrace("(-)[MODELSTATE_INVALID]");
                return ModelStateErrors.BuildErrorResponse(this.ModelState);
            }

            uint160 addressNumeric = request.ContractAddress.ToUint160(this.network);
            byte[] storageValue = this.stateRoot.GetStorageValue(addressNumeric, Encoding.UTF8.GetBytes(request.StorageKey));

            if (storageValue == null)
            {
                return this.Json(new
                {
                    Message = string.Format("No data at storage with key {0}", request.StorageKey)
                });
            }

            // Interpret the storage bytes as an object of the given type
            object interpretedStorageValue = this.InterpretStorageValue(request.DataType, storageValue);

            // Use MethodParamStringSerializer to serialize the interpreted object to a string
            string serialized = MethodParameterStringSerializer.Serialize(interpretedStorageValue, this.network);
            return this.Json(serialized);
        }

        /// <summary>
        /// Gets a smart contract transaction receipt. Receipts contain information about how a smart contract transaction was executed.
        /// This includes the value returned from a smart contract call and how much gas was used.  
        /// </summary>
        /// 
        /// <param name="txHash">A hash of the smart contract transaction (the transaction ID).</param>
        /// 
        /// <returns>The receipt for the smart contract.</returns> 
        [Route("receipt")]
        [HttpGet]
        public IActionResult GetReceipt([FromQuery] string txHash)
        {
            uint256 txHashNum = new uint256(txHash);
            Receipt receipt = this.receiptRepository.Retrieve(txHashNum);

            if (receipt == null)
            {
                this.logger.LogTrace("(-)[RECEIPT_NOT_FOUND]");
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest,
                    "The receipt was not found.",
                    "No stored transaction could be found for the supplied hash.");
            }

            uint160 address = receipt.NewContractAddress ?? receipt.To;

            if (!receipt.Logs.Any())
            {
                return this.Json(new ReceiptResponse(receipt, new List<LogResponse>(), this.network));
            }

            byte[] contractCode = this.stateRoot.GetCode(address);

            Assembly assembly = Assembly.Load(contractCode);

            var deserializer = new ApiLogDeserializer(this.primitiveSerializer, this.network);

            List<LogResponse> logResponses = this.MapLogResponses(receipt, assembly, deserializer);

            var receiptResponse = new ReceiptResponse(receipt, logResponses, this.network);

            return this.Json(receiptResponse);
        }

        // Note: We may not know exactly how to best structure "receipt search" queries until we start building 
        // a web3-like library. For now the following method serves as a very basic example of how we can query the block
        // bloom filters to retrieve events.


        /// <summary>
        /// Searches a smart contract's receipts for those which match a specific event. The SmartContract.Log() function
        /// is capable of storing C# structs, and structs are used to store information about different events occurring 
        /// on the smart contract. For example, a "TransferLog" struct could contain "From" and "To" fields and be used to log
        /// when a smart contract makes a transfer of funds from one wallet to another. The log entries are held inside the smart contract,
        /// indexed using the name of the struct, and are linked to individual transaction receipts.
        /// Therefore, it is possible to return a smart contract's transaction receipts
        /// which match a specific event (as defined by the struct name).  
        /// </summary>
        /// 
        /// <param name="contractAddress">The address of the smart contract to retrieve the receipts for.</param>
        /// <param name="eventName">The name of the event struct to retrieve matching receipts for.</param>
        /// 
        /// <returns>A list of receipts for transactions relating to a specific smart contract and a specific event in that smart contract.</returns>
        [Route("receipt-search")]
        [HttpGet]
        public async Task<IActionResult> ReceiptSearch([FromQuery] string contractAddress, [FromQuery] string eventName)
        {
            uint160 address = contractAddress.ToUint160(this.network);

            byte[] contractCode = this.stateRoot.GetCode(address);

            if (contractCode == null || !contractCode.Any())
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.InternalServerError, "No code exists", $"No contract execution code exists at {address}");
            }

            Assembly assembly = Assembly.Load(contractCode);

            var deserializer = new ApiLogDeserializer(this.primitiveSerializer, this.network);

            List<Receipt> receipts = this.SearchReceipts(contractAddress, eventName);

            var result = new List<ReceiptResponse>();

            foreach (Receipt receipt in receipts)
            {
                List<LogResponse> logResponses = this.MapLogResponses(receipt, assembly, deserializer);

                var receiptResponse = new ReceiptResponse(receipt, logResponses, this.network);

                result.Add(receiptResponse);
            }

            return this.Json(result);
        }

        private List<LogResponse> MapLogResponses(Receipt receipt, Assembly assembly, ApiLogDeserializer deserializer)
        {
            var logResponses = new List<LogResponse>();

            foreach (Log log in receipt.Logs)
            {
                var logResponse = new LogResponse(log, this.network);

                logResponses.Add(logResponse);

                if (log.Topics.Count == 0)
                    continue;

                // Get receipt struct name
                string eventTypeName = Encoding.UTF8.GetString(log.Topics[0]);

                // Find the type in the module def
                Type eventType = assembly.DefinedTypes.FirstOrDefault(t => t.Name == eventTypeName);

                if (eventType == null)
                {
                    // Couldn't match the type, continue?
                    continue;
                }

                // Deserialize it
                dynamic deserialized = deserializer.DeserializeLogData(log.Data, eventType);

                logResponse.Log = deserialized;
            }

            return logResponses;
        }

        private List<Receipt> SearchReceipts(string contractAddress, string eventName)
        {
            // Build the bytes we can use to check for this event.
            uint160 addressUint160 = contractAddress.ToUint160(this.network);
            byte[] addressBytes = addressUint160.ToBytes();
            byte[] eventBytes = Encoding.UTF8.GetBytes(eventName);

            // Loop through all headers and check bloom.
            IEnumerable<ChainedHeader> blockHeaders = this.chainIndexer.EnumerateToTip(this.chainIndexer.Genesis);
            List<ChainedHeader> matches = new List<ChainedHeader>();
            foreach (ChainedHeader chainedHeader in blockHeaders)
            {
                var scHeader = (ISmartContractBlockHeader)chainedHeader.Header;
                if (scHeader.LogsBloom.Test(addressBytes) && scHeader.LogsBloom.Test(eventBytes)) // TODO: This is really inefficient, should build bloom for query and then compare.
                    matches.Add(chainedHeader);
            }

            // For all matching headers, get the block from local db.
            List<NBitcoin.Block> blocks = new List<NBitcoin.Block>();
            foreach (ChainedHeader chainedHeader in matches)
            {
                blocks.Add(this.blockStore.GetBlock(chainedHeader.HashBlock));
            }

            // For each block, get all receipts, and if they match, add to list to return.
            List<Receipt> receiptResponses = new List<Receipt>();

            foreach (NBitcoin.Block block in blocks)
            {
                foreach (Transaction transaction in block.Transactions)
                {
                    Receipt storedReceipt = this.receiptRepository.Retrieve(transaction.GetHash());
                    if (storedReceipt == null) // not a smart contract transaction. Move to next transaction.
                        continue;

                    // Check if address and first topic (event name) match.
                    if (storedReceipt.Logs.Any(x =>
                        x.Address == addressUint160 && Enumerable.SequenceEqual(x.Topics[0], eventBytes)))
                        receiptResponses.Add(storedReceipt);
                }
            }

            return receiptResponses;
        }

        /// <summary>
        /// Gets the bytecode for a smart contract as a hexadecimal string. The bytecode is decompiled to
        /// C# source, which is returned as well. Be aware, it is the bytecode which is being executed,
        /// so this is the "source of truth".
        /// </summary>
        ///
        /// <param name="address">The address of the smart contract to retrieve as bytecode and C# source.</param>
        ///
        /// <returns>A response object containing the bytecode and the decompiled C# code.</returns>
        [Route("code")]
        [HttpGet]
        public IActionResult GetCode([FromQuery]string address)
        {
            uint160 addressNumeric = address.ToUint160(this.network);
            byte[] contractCode = this.stateRoot.GetCode(addressNumeric);

            if (contractCode == null || !contractCode.Any())
            {
                return this.Json(new GetCodeResponse
                {
                    Message = string.Format("No contract execution code exists at {0}", address)
                });
            }

            string typeName = this.stateRoot.GetContractType(addressNumeric);

            Result<string> sourceResult = this.contractDecompiler.GetSource(contractCode);

            return this.Json(new GetCodeResponse
            {
                Message = string.Format("Contract execution code retrieved at {0}", address),
                Bytecode = contractCode.ToHexString(),
                Type = typeName,
                CSharp = sourceResult.IsSuccess ? sourceResult.Value : sourceResult.Error // Show the source, or the reason why the source couldn't be retrieved.
            });
        }

        private object InterpretStorageValue(MethodParameterDataType dataType, byte[] bytes)
        {
            switch (dataType)
            {
                case MethodParameterDataType.Bool:
                    return this.serializer.ToBool(bytes);
                case MethodParameterDataType.Byte:
                    return bytes[0];
                case MethodParameterDataType.Char:
                    return this.serializer.ToChar(bytes);
                case MethodParameterDataType.String:
                    return this.serializer.ToString(bytes);
                case MethodParameterDataType.UInt:
                    return this.serializer.ToUInt32(bytes);
                case MethodParameterDataType.Int:
                    return this.serializer.ToInt32(bytes);
                case MethodParameterDataType.ULong:
                    return this.serializer.ToUInt64(bytes);
                case MethodParameterDataType.Long:
                    return this.serializer.ToInt64(bytes);
                case MethodParameterDataType.Address:
                    return this.serializer.ToAddress(bytes);
                case MethodParameterDataType.ByteArray:
                    return bytes.ToHexString();
            }

            return null;
        }
    }
}

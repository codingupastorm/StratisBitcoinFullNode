using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    /// <summary>
    /// Validates that a smart contract transaction can be deserialized correctly, and that it conforms to gas
    /// price and gas limit rules.
    /// </summary>
    public sealed class IsSmartContractWellFormedMempoolRule : MempoolRule
    {
        private readonly ICallDataSerializer callDataSerializer;

        public IsSmartContractWellFormedMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory,
            ICallDataSerializer callDataSerializer) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
            this.callDataSerializer = callDataSerializer;
        }

        /// <inheritdoc/>
        public override void CheckTransaction(MempoolValidationContext context)
        {
            // If the TxOut is null then this transaction does not contain any smart contract execution code.
            TxOut scTxOut = context.Transaction.TryGetSmartContractTxOut();
            if (scTxOut == null)
                return;

            ContractTransactionChecker.GetContractTxData(this.callDataSerializer, scTxOut);
        }
    }
}
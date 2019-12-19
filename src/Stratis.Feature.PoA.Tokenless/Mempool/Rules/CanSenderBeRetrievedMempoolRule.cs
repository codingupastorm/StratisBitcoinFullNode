using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    /// <summary>
    /// Checks that the sender can be retrieved from the signature in the <see cref="TxIn"></see> as well as checking that they have the required CA role for CREATE or CALL.
    /// </summary>
    public sealed class CanSenderBeRetrievedMempoolRule : MempoolRule
    {
        private readonly ITokenlessSigner tokenlessSigner;

        public CanSenderBeRetrievedMempoolRule(
            Network network,
            ITxMempool mempool,
            MempoolSettings settings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory,
            ITokenlessSigner tokenlessSigner)
            : base(network, mempool, settings, chainIndexer, loggerFactory)
        {
            this.tokenlessSigner = tokenlessSigner;
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            GetSenderResult getSenderResult = this.tokenlessSigner.GetSender(context.Transaction);
            if (!getSenderResult.Success)
                context.State.Fail(new MempoolError(MempoolErrors.RejectInvalid, "cannot-derive-sender-for-transaction"), $"Cannot derive the sender from transaction '{context.Transaction.GetHash()}': {getSenderResult.Error}").Throw();
        }
    }
}

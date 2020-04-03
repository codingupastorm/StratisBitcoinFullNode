using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    /// <summary>
    /// Checks whether the sender has not had it's certificate revoked.
    /// <para>
    /// If the CA is down or we cannot determin the certificate's status, we reject the transaction.
    /// </para>
    /// </summary>
    public sealed class CheckSenderCertificateIsNotRevoked : MempoolRule
    {
        private readonly ICertificatesManager certificateManager;
        private readonly ITokenlessSigner tokenlessSigner;

        public CheckSenderCertificateIsNotRevoked(
            Network network,
            ITxMempool mempool,
            MempoolSettings settings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory,
            ICertificatesManager certificateManager,
            ITokenlessSigner tokenlessSigner) : base(network, mempool, settings, chainIndexer, loggerFactory)
        {
            this.certificateManager = certificateManager;
            this.tokenlessSigner = tokenlessSigner;
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            // Firstly we need to check that the transaction is in the correct format. Can we get the sender?
            GetSenderResult getSenderResult = this.tokenlessSigner.GetSender(context.Transaction);
            if (!getSenderResult.Success)
                context.State.Fail(new MempoolError(MempoolErrors.RejectInvalid, "cannot-derive-sender-for-transaction"), $"Cannot derive the sender from transaction '{context.Transaction.GetHash()}': {getSenderResult.Error}").Throw();

            // Then check if the sender has not had it's certificate revoked.
            if (this.certificateManager.IsCertificateRevokedByAddress(getSenderResult.Sender))
                context.State.Fail(new MempoolError(MempoolErrors.RejectInvalid, "sender-certificate-is-revoked"), $"Cannot send transaction '{context.Transaction.GetHash()}' as the sender '{getSenderResult.Sender}', has had it's certificate revoked or the CA is uncontactable to determine it's status.").Throw();
        }
    }
}

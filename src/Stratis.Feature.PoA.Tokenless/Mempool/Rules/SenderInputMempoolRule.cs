using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    /// <summary>
    /// Checks that the sender can be retrieved from the signature in the <see cref="TxIn"></see> as well as checking that they have the required CA role for CREATE or CALL.
    /// </summary>
    public sealed class SenderInputMempoolRule : MempoolRule
    {
        private readonly ITokenlessSigner tokenlessSigner;

        public SenderInputMempoolRule(
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
            // Firstly we need to check that the transaction is in the correct format. Can we get the sender?
            GetSenderResult getSenderResult = this.tokenlessSigner.GetSender(context.Transaction);
            if (!getSenderResult.Success)
                new ConsensusError("error-getting-sender", string.Format("Unable to determine the sender for transaction '{0}'.", transaction.GetHash())).Throw();

            // We also need to check that the sender given is indeed the one who signed the transaction.
            if (!this.tokenlessSigner.Verify(context.Transaction))
                new ConsensusError("error-signature-invalid", $"The signature for transaction {context.Transaction.GetHash()} is invalid.");

            // Now that we have the sender address, lets get their certificate.
            // Note that we can do for other permissions too. Contract permissions etc.
            if (!this.certificateChecker.CheckSenderCertificateHasPermission(getSenderResult.Sender))

                GetSenderResult getSenderResult = this.tokenlessSigner.GetSender(context.Transaction);
            if (!getSenderResult.Success)
                context.State.Fail(new MempoolError(MempoolErrors.RejectInvalid, "cannot-derive-sender-for-transaction"), $"Cannot derive the sender from transaction '{context.Transaction.GetHash()}': {getSenderResult.Error}").Throw();

            // We also need to check that the sender given is indeed the one who signed the transaction.
            if (!this.tokenlessSigner.Verify(context.Transaction))
                context.State.Fail(new MempoolError(MempoolErrors.RejectInvalid, $"The signature for transaction {context.Transaction.GetHash()} is invalid."));
        }
    }
}

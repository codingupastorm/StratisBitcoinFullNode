using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    /// <summary>
    /// Checks that the sender can be retrieved from the signature in the <see cref="TxIn"></see> as well as checking that they have the required CA role for CREATE or CALL.
    /// </summary>
    public sealed class SenderInputMempoolRule : MempoolRule
    {
        private readonly ICertificatePermissionsChecker certificatePermissionsChecker;
        private readonly ITokenlessSigner tokenlessSigner;

        public SenderInputMempoolRule(
            Network network,
            ITxMempool mempool,
            MempoolSettings settings,
            ChainIndexer chainIndexer,
            ILoggerFactory loggerFactory,
            ITokenlessSigner tokenlessSigner,
            ICertificatePermissionsChecker certificatePermissionsChecker)
            : base(network, mempool, settings, chainIndexer, loggerFactory)
        {
            this.certificatePermissionsChecker = certificatePermissionsChecker;
            this.tokenlessSigner = tokenlessSigner;
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            // Firstly we need to check that the transaction is in the correct format. Can we get the sender?
            GetSenderResult getSenderResult = this.tokenlessSigner.GetSender(context.Transaction);
            if (!getSenderResult.Success)
                context.State.Fail(new MempoolError(MempoolErrors.RejectInvalid, "cannot-derive-sender-for-transaction"), $"Cannot derive the sender from transaction '{context.Transaction.GetHash()}': {getSenderResult.Error}").Throw();

            // We also need to check that the sender given is indeed the one who signed the transaction.
            if (!this.tokenlessSigner.Verify(context.Transaction))
                context.State.Fail(new MempoolError(MempoolErrors.RejectInvalid, $"The signature for transaction {context.Transaction.GetHash()} is invalid."));

            // Now that we have the sender address, lets get their certificate and check they have necessary permissions.
            if (!this.certificatePermissionsChecker.CheckSenderCertificateHasPermission(getSenderResult.Sender, TransactionSendingPermission.Send))
                context.State.Fail(new MempoolError(MempoolErrors.RejectInvalid, "The sender of this transaction is not authorised by the CA to send transactions."));

            // Not a smart contract, no further validation to do.
            if (!context.Transaction.IsSmartContractExecTransaction())
                return;

            TransactionSendingPermission permission = context.Transaction.IsSmartContractCreateTransaction()
                ? TransactionSendingPermission.CreateContract
                : TransactionSendingPermission.CallContract;

            if (!this.certificatePermissionsChecker.CheckSenderCertificateHasPermission(getSenderResult.Sender, permission))
                context.State.Fail(new MempoolError(MempoolErrors.RejectInvalid, $"The sender of this transaction does not have the {permission.ToString()} permission."));
        }
    }
}

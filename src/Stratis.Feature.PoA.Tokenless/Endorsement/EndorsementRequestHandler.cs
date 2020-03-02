using System;
using Stratis.SmartContracts.Core;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public class EndorsementRequestHandler
    {
        // TODO: read about endorsement of contracts.

        // TODO: work out: Does the original node send the read-write set with the transaction?

        // Receive request to sign transaction

        // Perform execution

        // Check that ReadWriteSet matches

        private readonly IEndorsementRequestValidator validator;
        private readonly IEndorsementSigner signer;
        private readonly IContractExecutor executor;

        public EndorsementRequestHandler(IEndorsementRequestValidator validator, IEndorsementSigner signer, IContractExecutor executor)
        {
            this.validator = validator;
            this.signer = signer;
            this.executor = executor;
        }

        public void ExecuteAndSignProposal(EndorsementRequest request)
        {
            if (!this.validator.ValidateRequest(request))
            {
                throw new NotImplementedException("What to do if proposal isn't valid.");
            }

            // TODO: Build execution context.
            // TODO: Check that multiple things don't execute in the executor at the same time.
            var executionContext = new ContractTransactionContext(0,0,0,0,0, null);
            IContractExecutionResult result = this.executor.Execute(executionContext);

            if (!result.Revert)
            {
                throw new NotImplementedException("Do we need to check if the transaction changes the correct things in the RWS?");
                this.signer.Sign(request);
            }

            throw new NotImplementedException("What to do if execution wasn't successful.");
        }
    }
}

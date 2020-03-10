using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementRequestValidator
    {
        bool ValidateRequest(EndorsementRequest request);
    }

    public class EndorsementRequestValidator : IEndorsementRequestValidator
    {
        private readonly IEndorsements endorsements;
        private readonly ITokenlessSigner tokenlessSigner;
        private readonly IReadWriteSetTransactionSerializer readWriteSetTransactionSerializer;

        /*
         * From HL docs:
         *
         * (1) that the transaction proposal is well formed,
         * (2) it has not been submitted already in the past (replay-attack protection),
         * (3) the signature is valid (using the MSP), and
         * (4) that the submitter (Client A, in the example) is properly authorized to perform the proposed operation on that channel (namely, each endorsing peer ensures that the submitter satisfies the channel’s Writers policy)
         *
         */

        public EndorsementRequestValidator(IEndorsements endorsements, ITokenlessSigner tokenlessSigner, IReadWriteSetTransactionSerializer readWriteSetTransactionSerializer)
        {
            this.endorsements = endorsements;
            this.tokenlessSigner = tokenlessSigner;
            this.readWriteSetTransactionSerializer = readWriteSetTransactionSerializer;
        }

        public bool ValidateRequest(EndorsementRequest request)
        {
            // Confirm that the transaction hash has not been encountered before.
            uint256 proposalId = request.ContractTransaction.GetHash();
            if (this.endorsements.GetEndorsement(proposalId) != null)
                return false;

            this.endorsements.RecordEndorsement(proposalId);

            // Check that the transaction proposal is well formed.
            if (request.ContractTransaction.Inputs.Count != 1)
                return false;

            if (request.ContractTransaction.Outputs.Count != 1)
                return false;

            // Verify that the signature is valid.
            this.tokenlessSigner.Verify(request.ContractTransaction);

            // Check that the submitter is properly authorized.
            GetSenderResult res = this.tokenlessSigner.GetSender(request.ContractTransaction);
            if (!res.Success)
                return false;

            // TODO: Check that the submitter (Client A, in the example) is properly authorized to perform the 
            // proposed operation on that channel (namely, each endorsing peer ensures that the submitter 
            // satisfies the channel’s Writers policy). Perhaps we could add a method to EndorsementSigner.

            return true;
        }

    }
}

using System.Collections.Generic;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementRequestValidator
    {
        bool ValidateRequest(EndorsementRequest request);
    }

    public class EndorsementRequestValidator : IEndorsementRequestValidator
    {
        private readonly HashSet<uint256> alreadySeen;
        private readonly ITokenlessSigner tokenlessSigner;

        /*
         * From HL docs:
         *
         * (1) that the transaction proposal is well formed,
         * (2) it has not been submitted already in the past (replay-attack protection),
         * (3) the signature is valid (using the MSP), and
         * (4) that the submitter (Client A, in the example) is properly authorized to perform the proposed operation on that channel (namely, each endorsing peer ensures that the submitter satisfies the channel’s Writers policy)
         *
         */

        public EndorsementRequestValidator(ITokenlessSigner tokenlessSigner)
        {
            this.alreadySeen = new HashSet<uint256>();
            this.tokenlessSigner = tokenlessSigner;
        }

        public bool ValidateRequest(EndorsementRequest request)
        {
            // Check that the transaction proposal is well formed.
            if (request.ContractTransaction.Inputs.Count != 1)
                return false;

            if (request.ContractTransaction.Outputs.Count != 1)
                return false;

            // TODO: Check the RWS.

            // Confirm that the transaction hash has not been encountered before.
            if (this.alreadySeen.Contains(request.ContractTransaction.GetHash()))
                return false;

            this.alreadySeen.Add(request.ContractTransaction.GetHash());

            // Verify that the signature is valid.
            this.tokenlessSigner.Verify(request.ContractTransaction);

            // Check that the submitter is properly authorized.
            GetSenderResult res = this.tokenlessSigner.GetSender(request.ContractTransaction);
            if (!res.Success)
                return false;

            // TODO: Check that the submitter (Client A, in the example) is properly authorized to perform the 
            // proposed operation on that channel (namely, each endorsing peer ensures that the submitter 
            // satisfies the channel’s Writers policy)

            return true;
        }

    }
}

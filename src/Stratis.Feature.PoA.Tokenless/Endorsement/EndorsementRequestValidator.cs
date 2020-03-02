using System;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public class EndorsementRequestValidator : IEndorsementRequestValidator
    {
        /*
         * From HL docs:
         *
         * (1) that the transaction proposal is well formed,
         * (2) it has not been submitted already in the past (replay-attack protection),
         * (3) the signature is valid (using the MSP), and
         * (4) that the submitter (Client A, in the example) is properly authorized to perform the proposed operation on that channel (namely, each endorsing peer ensures that the submitter satisfies the channel’s Writers policy)
         *
         */

        public bool ValidateRequest(EndorsementRequest request)
        {
            throw new NotImplementedException("Check transaction vs rules.");
        }

    }

    public interface IEndorsementRequestValidator
    {
        bool ValidateRequest(EndorsementRequest request);
    }
}

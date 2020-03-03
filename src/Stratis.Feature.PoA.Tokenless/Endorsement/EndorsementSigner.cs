using System;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementSigner
    {
        void Sign(EndorsementRequest request);
    }

    public class EndorsementSigner : IEndorsementSigner
    {
        public void Sign(EndorsementRequest request)
        {
            throw new NotImplementedException("Work out how signing process is intended to work and then sign using key from this node.");
        }
    }
}

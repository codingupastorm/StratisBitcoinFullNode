using System;

namespace Stratis.Features.Consensus.ProvenBlockHeaders
{
    public class ProvenBlockHeaderException : Exception
    {
        public ProvenBlockHeaderException(string message) : base(message)
        {
        }
    }
}

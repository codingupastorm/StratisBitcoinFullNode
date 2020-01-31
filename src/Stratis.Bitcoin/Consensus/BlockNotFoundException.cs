using System;

namespace Stratis.Bitcoin.Consensus
{
    public class BlockNotFoundException : Exception
    {
        public BlockNotFoundException(string message) : base(message)
        {
        }
    }
}

using System;

namespace Stratis.Features.BlockStore
{
    public class BlockStoreException : Exception
    {
        public BlockStoreException(string message) : base(message)
        {
        }
    }
}

using System;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public sealed class ChannelServiceException : Exception
    {
        public ChannelServiceException(string message) : base(message)
        {
        }
    }
}

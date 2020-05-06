using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Core.AsyncWork.Extensions
{
    public static class PeerExtensions
    {
        public static bool IsWhitelisted(this INetworkPeer peer)
        {
            return peer.Behavior<IConnectionManagerBehavior>()?.Whitelisted == true;
        }
    }
}

using Stratis.Core.Connection;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Core.Utilities.Extensions
{
    public static class PeerExtensions
    {
        public static bool IsWhitelisted(this INetworkPeer peer)
        {
            return peer.Behavior<IConnectionManagerBehavior>()?.Whitelisted == true;
        }
    }
}

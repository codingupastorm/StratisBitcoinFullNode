using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public class EndorsementRequest
    {
        public Transaction ContractTransaction { get; set; }

        // The peer providing the endorsement request.
        // We wil be responding back to this peer.
        public INetworkPeer Peer;

        public byte[] TransientData { get; set; }
    }
}

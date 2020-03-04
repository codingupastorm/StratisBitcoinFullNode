using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface ITokenlessBroadcaster
    {
        /// <summary>
        /// Broadcasts a message to the first peer in a specific organisation.
        /// </summary>
        Task BroadcastToFirstInOrganisationAsync(Payload payload, string organisation);
    }

    /// <summary>
    /// Holds any means of selectively broadcasting information throughout tokenless networks.
    /// </summary>
    public class TokenlessBroadcaster : ITokenlessBroadcaster
    {
        private readonly IConnectionManager connectionManager;

        /// <summary>
        /// A list of all peers and their client certificate.
        /// </summary>
        private List<(INetworkPeer Peer, X509Certificate Certificate)> PeersWithCerts => 
            this.connectionManager.ConnectedPeers.Select(x => (x, (x.Connection as TlsEnabledNetworkPeerConnection).GetPeerCertificate())).ToList();

        public TokenlessBroadcaster(IConnectionManager connectionManager)
        {
            this.connectionManager = connectionManager;
        }

        /// <inheritdoc />
        public async Task BroadcastToFirstInOrganisationAsync(Payload payload, string organisation)
        {
            IEnumerable<(INetworkPeer Peer, X509Certificate Certificate)> peersInOrganisation = this.PeersWithCerts.Where(x => GetOrganisation(x.Certificate) == organisation);

            // For now, only take the first in the organisation. As endorsement grows more fully-featured we can adjust.
            INetworkPeer peer = peersInOrganisation.FirstOrDefault().Peer;

            try
            {
                await peer.SendMessageAsync(payload).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // This catch is a bit dirty but is copied from FederatedPegBroadcaster code.
            }
        }

        private static string GetOrganisation(X509Certificate certificate)
        {
            return certificate.SubjectDN.GetValueList(X509Name.O).OfType<string>().First();
        }
    }
}

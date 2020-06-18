using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CertificateAuthority;
using MembershipServices;
using Org.BouncyCastle.X509;
using Stratis.Core.Connection;
using Stratis.Core.P2P.Peer;
using Stratis.Core.P2P.Protocol.Payloads;
using Stratis.Feature.PoA.Tokenless.ProtocolEncryption;
using Stratis.SmartContracts.Core.AccessControl;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface ITokenlessBroadcaster
    {
        /// <summary>
        /// Broadcasts a message to the first peer in a specific organisation.
        /// </summary>
        Task BroadcastToFirstInOrganisationAsync(Payload payload, string organisation);

        /// <summary>
        /// Broadcasts a message to every peer in a specific organisation. If no organisation is passed in then this node's organisation will be used.
        /// </summary>
        Task BroadcastToOrganisationAsync(Payload payload, string organisation = null);

        /// <summary>
        /// Broadcasts a message to a peer identified by a thumbprint if they exist.
        /// </summary>
        Task BroadcastToThumbprintAsync(Payload payload, string thumbprint);

        /// <summary>
        /// Broadcasts a message to every peer identified by an AccessControlList.
        /// </summary>
        Task BroadcastToAccessControlListAsync(Payload payload, AccessControlList acl);
    }

    /// <summary>
    /// Holds any means of selectively broadcasting information throughout tokenless networks.
    /// </summary>
    public class TokenlessBroadcaster : ITokenlessBroadcaster
    {
        private readonly IConnectionManager connectionManager;

        private readonly IMembershipServicesDirectory membershipServices;

        /// <summary>
        /// A list of all peers and their client certificate.
        /// </summary>
        private List<(INetworkPeer Peer, X509Certificate Certificate)> PeersWithCerts =>
            this.connectionManager.ConnectedPeers.Select(x => (x, (x.Connection as TlsEnabledNetworkPeerConnection).GetPeerCertificate())).ToList();

        public TokenlessBroadcaster(IConnectionManager connectionManager, IMembershipServicesDirectory membershipServices)
        {
            this.connectionManager = connectionManager;
            this.membershipServices = membershipServices;
        }

        /// <inheritdoc />
        public async Task BroadcastToFirstInOrganisationAsync(Payload payload, string organisation)
        {
            IEnumerable<(INetworkPeer Peer, X509Certificate Certificate)> peersInOrganisation = this.GetPeersForOrganisation(organisation);

            // TODO: Error handling. What if we're not connected to an endorser?

            INetworkPeer peer = peersInOrganisation.First().Peer;

            await SendMessageToPeerAsync(peer, payload);
        }

        /// <inheritdoc />
        public async Task BroadcastToOrganisationAsync(Payload payload, string organisation = null)
        {
            if (organisation == null)
            {
                organisation = this.membershipServices.ClientCertificate.GetOrganisation();
            }

            IEnumerable<(INetworkPeer Peer, X509Certificate Certificate)> peers = this.GetPeersForOrganisation(organisation);

            // TODO: Error handling. What if we don't have any peers in the same organisation?

            Parallel.ForEach(peers.Select(x => x.Peer), async (INetworkPeer peer) => await SendMessageToPeerAsync(peer, payload));
        }

        public async Task BroadcastToThumbprintAsync(Payload payload, string thumbprint)
        {
            (INetworkPeer Peer, X509Certificate Certificate) peer = this.GetPeerByThumbprint(thumbprint);

            if (peer.Peer != null)
                await SendMessageToPeerAsync(peer.Peer, payload);
        }

        public async Task BroadcastToAccessControlListAsync(Payload payload, AccessControlList acl)
        {
            IEnumerable<(INetworkPeer Peer, X509Certificate Certificate)> peers = this.GetPeersForAccessList(acl);

            Parallel.ForEach(peers.Select(x => x.Peer), async (INetworkPeer peer) => await SendMessageToPeerAsync(peer, payload));
        }

        private async Task SendMessageToPeerAsync(INetworkPeer peer, Payload payload)
        {
            try
            {
                await peer.SendMessageAsync(payload).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // This catch is a bit dirty but is copied from FederatedPegBroadcaster code.
            }
        }

        private IEnumerable<(INetworkPeer Peer, X509Certificate Certificate)> GetPeersForAccessList(AccessControlList acl)
        {
            return this.PeersWithCerts.Where(x =>
                acl.Thumbprints.Contains(CaCertificatesManager.GetThumbprint(x.Certificate))
                || acl.Organisations.Contains(x.Certificate.GetOrganisation()));
        }

        private IEnumerable<(INetworkPeer Peer, X509Certificate Certificate)> GetPeersForOrganisation(string organisation)
        {
            return this.PeersWithCerts.Where(x => x.Certificate.GetOrganisation() == organisation);
        }

        private (INetworkPeer Peer, X509Certificate Certificate) GetPeerByThumbprint(string thumbprint)
        {
            return this.PeersWithCerts.FirstOrDefault(x =>
                CaCertificatesManager.GetThumbprint(x.Certificate) == thumbprint);
        }
    }
}

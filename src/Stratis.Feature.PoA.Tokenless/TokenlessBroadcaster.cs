﻿using System;
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

        /// <summary>
        /// Broadcasts a message to every peer in a specific organisation. If no organisation is passed in then this node's organisation will be used.
        /// </summary>
        Task BroadcastToWholeOrganisationAsync(Payload payload, string organisation = null);
    }

    /// <summary>
    /// Holds any means of selectively broadcasting information throughout tokenless networks.
    /// </summary>
    public class TokenlessBroadcaster : ITokenlessBroadcaster
    {
        private readonly IConnectionManager connectionManager;

        private readonly ICertificatesManager certificatesManager;

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
            IEnumerable<(INetworkPeer Peer, X509Certificate Certificate)> peersInOrganisation = this.GetPeersForOrganisation(organisation);

            // TODO: Error handling. What if we're not connected to an endorser?

            INetworkPeer peer = peersInOrganisation.First().Peer;

            await SendMessageToPeerAsync(peer, payload);
        }

        /// <inheritdoc />
        public async Task BroadcastToWholeOrganisationAsync(Payload payload, string organisation = null)
        {
            if (organisation == null)
            {
                string thisNodesOrganisation = GetOrganisation(this.certificatesManager.ClientCertificate);
            }

            IEnumerable<(INetworkPeer Peer, X509Certificate Certificate)> peers = this.GetPeersForOrganisation(organisation);

            // TODO: Error handling. What if we don't have any peers in the same organisation?

            Parallel.ForEach<INetworkPeer>(peers.Select(x=>x.Peer), async (INetworkPeer peer) => await SendMessageToPeerAsync(peer, payload));
        }

        private async Task SendMessageToPeerAsync(INetworkPeer peer, Payload payload)
        {
            try
            {
                await peer.SendMessageAsync(payload).ConfigureAwait(false);
            }
            catch (OperationCanceledException e)
            {
                // This catch is a bit dirty but is copied from FederatedPegBroadcaster code.
            }
        }

        private IEnumerable<(INetworkPeer Peer, X509Certificate Certificate)> GetPeersForOrganisation(string organisation)
        {
            return this.PeersWithCerts.Where(x => GetOrganisation(x.Certificate) == organisation);
        }

        private static string GetOrganisation(X509Certificate certificate)
        {
            return certificate.SubjectDN.GetValueList(X509Name.O).OfType<string>().First();
        }
    }
}

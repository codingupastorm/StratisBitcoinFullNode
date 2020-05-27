﻿using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.AsyncWork;
using Stratis.Core.Configuration;
using Stratis.Core.Configuration.Settings;
using Stratis.Core.P2P.Peer;
using Stratis.Core.P2P.Protocol.Payloads;
using Stratis.Core.Utilities;
using Stratis.Core.Utilities.Extensions;
using TracerAttributes;

namespace Stratis.Core.P2P
{
    /// <summary>
    /// The connector used to connect to peers specified with the -connect argument
    /// </summary>
    public sealed class PeerConnectorConnectNode : PeerConnector
    {
        private readonly ILogger logger;

        public PeerConnectorConnectNode(
            IAsyncProvider asyncProvider,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifetime,
            NodeSettings nodeSettings,
            ConnectionManagerSettings connectionSettings,
            IPeerAddressManager peerAddressManager,
            ISelfEndpointTracker selfEndpointTracker) :
            base(asyncProvider, dateTimeProvider, loggerFactory, network, networkPeerFactory, nodeLifetime, nodeSettings, connectionSettings, peerAddressManager, selfEndpointTracker)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.Requirements.RequiredServices = NetworkPeerServices.Nothing;
        }

        /// <inheritdoc/>
        protected override void OnInitialize()
        {
            this.MaxOutboundConnections = this.ConnectionSettings.Connect.Count;

            // Add the endpoints from the -connect arg to the address manager.
            foreach (IPEndPoint ipEndpoint in this.ConnectionSettings.Connect)
            {
                this.PeerAddressManager.AddPeer(ipEndpoint.MapToIpv6(), IPAddress.Loopback);
            }
        }

        /// <summary>This connector is only started if there are peers in the -connect args.</summary>
        public override bool CanStartConnect
        {
            get { return this.ConnectionSettings.Connect.Any(); }
        }

        /// <inheritdoc/>
        [NoTrace]
        protected override void OnStartConnect()
        {
            this.CurrentParameters.PeerAddressManagerBehaviour().Mode = PeerAddressManagerBehaviourMode.None;
        }

        /// <inheritdoc/>
        [NoTrace]
        protected override TimeSpan CalculateConnectionInterval()
        {
            return TimeSpans.Second;
        }

        /// <summary>
        /// Only connect to nodes as specified in the -connect node arg.
        /// </summary>
        public override async Task OnConnectAsync()
        {
            await this.ConnectionSettings.Connect.ForEachAsync(this.ConnectionSettings.MaxOutboundConnections, this.NodeLifetime.ApplicationStopping,
                async (ipEndpoint, cancellation) =>
                {
                    if (this.NodeLifetime.ApplicationStopping.IsCancellationRequested)
                        return;

                    PeerAddress peerAddress = this.PeerAddressManager.FindPeer(ipEndpoint);
                    if (peerAddress != null)
                    {
                        this.logger.LogDebug("Attempting connection to {0}.", peerAddress.Endpoint);

                        await this.ConnectAsync(peerAddress).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
        }
    }
}
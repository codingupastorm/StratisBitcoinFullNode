using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Core.AsyncWork;
using Stratis.Core.Connection;
using Stratis.Core.Utilities;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Models;
using Stratis.Features.Tokenless.Channels;

namespace Stratis.Feature.Tokenless.Channels
{
    /// <summary>
    /// Contract for <see cref="SystemChannelAddressRetriever"/>.
    /// </summary>
    public interface ISystemChannelAddressRetriever : IDisposable
    {
        /// <summary>
        /// Starts the async loop which retrieves system channel addresses from the infra node.
        /// </summary>
        void Retrieve();
    }

    /// <inheritdoc/>
    public sealed class SystemChannelAddressRetriever : ISystemChannelAddressRetriever
    {
        private readonly IAsyncProvider asyncProvider;
        private readonly IConnectionManager connectionManager;
        private readonly ChannelSettings channelSettings;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly INodeLifetime nodeLifetime;
        private IAsyncLoop retrieveSystemChannelNodesLoop;

        public SystemChannelAddressRetriever(
            IAsyncProvider asyncProvider,
            ChannelSettings channelSettings,
            IConnectionManager connectionManager,
            IHttpClientFactory httpClientFactory,
            ILoggerFactory loggerFactory,
            INodeLifetime nodeLifetime)
        {
            this.asyncProvider = asyncProvider;
            this.channelSettings = channelSettings;
            this.connectionManager = connectionManager;
            this.httpClientFactory = httpClientFactory;
            this.loggerFactory = loggerFactory;
            this.nodeLifetime = nodeLifetime;

            this.logger = this.loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc/>
        public void Retrieve()
        {
            if (!this.channelSettings.IsSystemChannelNode)
            {
                this.logger.LogDebug($"{nameof(SystemChannelAddressRetriever)} will not start as this is not a system channel node.");
                return;
            }

            if (string.IsNullOrEmpty(this.channelSettings.InfraNodeApiUri))
            {
                this.logger.LogDebug($"{nameof(SystemChannelAddressRetriever)} will not start as the infranode's URI or port number is null or invalid.");
                return;
            }

            this.retrieveSystemChannelNodesLoop = this.asyncProvider.CreateAndRunAsyncLoop(nameof(this.RetrieveSystemChannelNodeAddressesAsync), async token =>
            {
                await this.RetrieveSystemChannelNodeAddressesAsync();
            },
            this.nodeLifetime.ApplicationStopping,
            TimeSpan.FromHours(1));
        }

        /// <summary>
        /// See <see cref="Retrieve"/>. This loop deals with retrieving system channel node IPs from the Infra node.
        /// </summary>
        private async Task RetrieveSystemChannelNodeAddressesAsync()
        {
            using var connectTokenSource = CancellationTokenSource.CreateLinkedTokenSource(this.nodeLifetime.ApplicationStopping);

            this.logger.LogDebug("Attempting to retrieve system channel node addresses from : '{0}'", this.channelSettings.InfraNodeApiUri);

            var infraNodeClient = new InfraNodeApiClient(this.loggerFactory, this.httpClientFactory, new Uri(this.channelSettings.InfraNodeApiUri), "channels");
            SystemChannelAddressesModel systemChannelAddresses = await infraNodeClient.SendGetRequestAsync<SystemChannelAddressesModel>("systemchanneladdresses", cancellation: connectTokenSource.Token);

            this.logger.LogDebug($"'{systemChannelAddresses.Addresses.Count}' system channel nodes addresses retrieved");

            foreach (var address in systemChannelAddresses.Addresses)
            {
                this.connectionManager.AddNodeAddress(IPEndPoint.Parse(address));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.retrieveSystemChannelNodesLoop?.Dispose();
        }
    }
}
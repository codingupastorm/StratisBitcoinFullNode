using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CertificateAuthority.Client;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.ProtocolEncryption
{
    public class RevocationChecker : IDisposable
    {
        private const string kvRepoKey = "revocedcerts";

        private readonly NodeSettings nodeSettings;

        private readonly IKeyValueRepository kvRepo;

        protected ILogger logger;

        private HashSet<string> revokedCertsCache;

        private Client client;

        private Task cacheUpdatingTask;

        private CancellationTokenSource cancellation;

        private readonly TimeSpan CacheUpdateInterval = TimeSpan.FromHours(12);

        public RevocationChecker(NodeSettings nodeSettings, IKeyValueRepository kvRepo, ILoggerFactory loggerFactory)
        {
            this.nodeSettings = nodeSettings;
            this.kvRepo = kvRepo;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.cancellation = new CancellationTokenSource();
        }

        public async Task InitializeAsync()
        {
            TextFileConfiguration config = this.nodeSettings.ConfigReader;
            string certificateAuthorityUrl = config.GetOrDefault<string>("caurl", "https://localhost:5001");

            this.client = new Client(certificateAuthorityUrl, new HttpClient());

            this.revokedCertsCache = this.kvRepo.LoadValueJson<HashSet<string>>(kvRepoKey);

            if (this.revokedCertsCache == null)
                await this.UpdateRevokedCertsCacheAsync().ConfigureAwait(false);

            this.cacheUpdatingTask = this.UpdateRevokedCertsCacheContinuouslyAsync();
        }

        public async Task<bool> IsCertificateRevokedAsync(string thumbprint)
        {
            // First try to ask CA server directly.
            try
            {
                var requestModel = new GetCertificateStatusModel() {AsString = true, Thumbprint = thumbprint};
                string status = await this.client.GetCertificateStatusAsync(requestModel).ConfigureAwait(false);

                return status != "Good";
            }
            catch (Exception e)
            {
                this.logger.LogDebug("Error while checking certificate status: '{0}'.", e.ToString());

                // Use cache.
                if ((this.revokedCertsCache != null) && this.revokedCertsCache.Contains(thumbprint))
                    return true;
            }

            return false;
        }

        private async Task UpdateRevokedCertsCacheAsync()
        {
            try
            {
                ICollection<string> result = await this.client.GetRevokedCertificatesAsync().ConfigureAwait(false);
                this.revokedCertsCache = new HashSet<string>(result);
            }
            catch (Exception e)
            {
                this.logger.LogWarning("Failed to reach certificate authority server.");
                this.logger.LogDebug(e.ToString());
            }
        }

        private async Task UpdateRevokedCertsCacheContinuouslyAsync()
        {
            while (!this.cancellation.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(this.CacheUpdateInterval, this.cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                await this.UpdateRevokedCertsCacheAsync().ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (this.revokedCertsCache != null)
                this.kvRepo.SaveValueJson(kvRepoKey, this.revokedCertsCache);

            this.cancellation.Cancel();
            this.cacheUpdatingTask?.GetAwaiter().GetResult();
        }
    }
}

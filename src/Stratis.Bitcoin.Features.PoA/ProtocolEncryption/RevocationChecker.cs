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
    internal class RevocationRecord
    {
        public DateTime LastChecked { get; set; }

        public bool LastStatus { get; set; }
    }

    public class RevocationChecker : IDisposable
    {
        private const string kvRepoKey = "revokedcerts";

        private readonly NodeSettings nodeSettings;

        private readonly IKeyValueRepository kvRepo;

        private readonly ILogger logger;

        private readonly IDateTimeProvider dateTimeProvider;

        private Dictionary<string, RevocationRecord> revokedCertsCache;

        private Client client;

        private Task cacheUpdatingTask;

        private CancellationTokenSource cancellation;

        private readonly TimeSpan cacheUpdateInterval = TimeSpan.FromHours(1);

        public RevocationChecker(NodeSettings nodeSettings, IKeyValueRepository kvRepo, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
        {
            this.nodeSettings = nodeSettings;
            this.kvRepo = kvRepo;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.cancellation = new CancellationTokenSource();
            this.dateTimeProvider = dateTimeProvider;

            // Ensure that the cache is never null, but it actually gets initialised from the repository later.
            this.revokedCertsCache = new Dictionary<string, RevocationRecord>();
        }

        public async Task InitializeAsync()
        {
            TextFileConfiguration config = this.nodeSettings.ConfigReader;
            string certificateAuthorityUrl = config.GetOrDefault<string>("caurl", "https://localhost:5001");

            this.client = new Client(certificateAuthorityUrl, new HttpClient());

            this.revokedCertsCache = this.kvRepo.LoadValueJson<Dictionary<string, RevocationRecord>>(kvRepoKey);

            if (this.revokedCertsCache == null)
                await this.UpdateRevokedCertsCacheAsync().ConfigureAwait(false);

            this.cacheUpdatingTask = this.UpdateRevokedCertsCacheContinuouslyAsync();
        }

        public async Task<bool> IsCertificateRevokedAsync(string thumbprint, bool allowCached = true)
        {
            RevocationRecord record = null;

            if (allowCached && this.revokedCertsCache.TryGetValue(thumbprint, out record))
            {
                if ((this.dateTimeProvider.GetUtcNow() - record.LastChecked) < this.cacheUpdateInterval)
                    return record.LastStatus;
            }

            if (record == null)
                record = new RevocationRecord() { LastChecked = this.dateTimeProvider.GetUtcNow(), LastStatus = false };

            // Cannot use cache, or no record existed yet. Ask CA server directly.
            try
            {
                string status = await this.client.Get_certificate_statusAsync(thumbprint, true).ConfigureAwait(false);

                record.LastChecked = this.dateTimeProvider.GetUtcNow();
                record.LastStatus = status != "Good";
            }
            catch (Exception e)
            {
                this.logger.LogDebug("Error while checking certificate status: '{0}'.", e.ToString());
            }

            this.revokedCertsCache[thumbprint] = record;

            return record.LastStatus;
        }

        private async Task UpdateRevokedCertsCacheAsync()
        {
            try
            {
                ICollection<string> result = await this.client.Get_revoked_certificatesAsync().ConfigureAwait(false);

                foreach (string identifier in result)
                {
                    // We don't actually care what the previous state was, only that it is revoked now.
                    this.revokedCertsCache[identifier] = new RevocationRecord() { LastChecked = this.dateTimeProvider.GetUtcNow(), LastStatus = true };
                }
            }
            catch (Exception e)
            {
                this.logger.LogWarning("Failed to reach certificate authority server.");
                this.logger.LogDebug(e.ToString());

                this.revokedCertsCache = new Dictionary<string, RevocationRecord>();
            }
        }

        private async Task UpdateRevokedCertsCacheContinuouslyAsync()
        {
            while (!this.cancellation.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(this.cacheUpdateInterval, this.cancellation.Token);
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
            this.kvRepo?.SaveValueJson(kvRepoKey, this.revokedCertsCache);
            this.cancellation.Cancel();
            this.cacheUpdatingTask?.GetAwaiter().GetResult();
        }
    }
}

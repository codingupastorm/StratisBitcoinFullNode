using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CertificateAuthority;
using CertificateAuthority.Models;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using TextFileConfiguration = Stratis.Bitcoin.Configuration.TextFileConfiguration;

namespace Stratis.Bitcoin.Features.PoA.ProtocolEncryption
{
    internal class RevocationRecord
    {
        public DateTime LastChecked { get; set; }

        public bool LastStatus { get; set; }
    }

    public interface IRevocationChecker : IDisposable
    {
        bool IsCertificateRevoked(string thumbprint, bool allowCached = true);

        void Initialize();
    }

    public sealed class RevocationChecker : IRevocationChecker
    {
        private const string kvRepoKey = "revokedcerts";

        private readonly IKeyValueRepository kvRepo;

        private readonly ILogger logger;

        private readonly IDateTimeProvider dateTimeProvider;

        private readonly TextFileConfiguration configuration;

        private string caUrl;
        private string caPassword;
        private int caAccountId;

        private Dictionary<string, RevocationRecord> revokedCertsCache;

        private CaClient client;

        private Task cacheUpdatingTask;

        private readonly CancellationTokenSource cancellationTokenSource;

        private readonly TimeSpan cacheUpdateInterval = TimeSpan.FromHours(1);

        public RevocationChecker(NodeSettings nodeSettings,
            IKeyValueRepository kvRepo,
            ILoggerFactory loggerFactory,
            IDateTimeProvider dateTimeProvider)
        {
            this.kvRepo = kvRepo;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.cancellationTokenSource = new CancellationTokenSource();
            this.dateTimeProvider = dateTimeProvider;

            // Ensure that the cache is never null, but it actually gets initialised from the repository later.
            this.revokedCertsCache = new Dictionary<string, RevocationRecord>();

            this.configuration = nodeSettings.ConfigReader;
        }

        public void Initialize()
        {
            // TODO: Create a common settings class that can be injected
            this.caUrl = this.configuration.GetOrDefault<string>("caurl", "https://localhost:5001");
            this.caPassword = this.configuration.GetOrDefault<string>(CertificatesManager.CaPasswordKey, null);
            this.caAccountId = this.configuration.GetOrDefault<int>(CertificatesManager.CaAccountIdKey, 0);

            this.client = this.GetClient();

            this.revokedCertsCache = this.kvRepo.LoadValueJson<Dictionary<string, RevocationRecord>>(kvRepoKey);

            if (this.revokedCertsCache == null)
            {
                this.revokedCertsCache = new Dictionary<string, RevocationRecord>();
                this.UpdateRevokedCertsCache();
            }

            this.cacheUpdatingTask = this.UpdateRevokedCertsCacheContinuouslyAsync();
        }

        public CaClient GetClient()
        {
            var httpClient = new HttpClient();

            return new CaClient(new Uri(this.caUrl), httpClient, this.caAccountId, this.caPassword);
        }

        /// <summary>
        /// As there can be multiple certificates in existence for a given P2PKH address (i.e. e.g. with different expiry dates), we determine revocation via thumbprint.
        /// </summary>
        /// <param name="thumbprint">The thumbprint of the certificate to check the revocation status of.</param>
        /// <param name="allowCached">Indicate whether it is acceptable to use the checker's local cache of the status, or force a check with the CA.</param>
        /// <returns><c>true</c> if the given certificate has been revoked.</returns>
        public bool IsCertificateRevoked(string thumbprint, bool allowCached = true)
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
                string status = this.client.GetCertificateStatus(thumbprint);

                record.LastChecked = this.dateTimeProvider.GetUtcNow();
                record.LastStatus = status != "1";
            }
            catch (Exception e)
            {
                this.logger.LogDebug("Error while checking certificate status: '{0}'.", e.ToString());
            }

            this.revokedCertsCache[thumbprint] = record;

            return record.LastStatus;
        }

        private void UpdateRevokedCertsCache()
        {
            try
            {
                // We should get the status of every certificate known to the CA, in case the CA is not online when a previously unseen certificate is first encountered later.
                // TODO: Perhaps certificate updates should be broadcast instead of potentially having a stale cache entry?
                List<CertificateInfoModel> certificateInfoModels = this.client.GetAllCertificates();
                foreach (CertificateInfoModel certificateInfoModel in certificateInfoModels)
                {
                    if (certificateInfoModel.Status == CertificateStatus.Good || certificateInfoModel.Status == CertificateStatus.Unknown)
                        this.revokedCertsCache[certificateInfoModel.Thumbprint] = new RevocationRecord() { LastChecked = this.dateTimeProvider.GetUtcNow(), LastStatus = false };
                    else
                        this.revokedCertsCache[certificateInfoModel.Thumbprint] = new RevocationRecord() { LastChecked = this.dateTimeProvider.GetUtcNow(), LastStatus = true };
                }
            }
            catch (Exception e)
            {
                this.logger.LogWarning("Failed to reach certificate authority server.");
                this.logger.LogDebug(e.ToString());
            }
        }

        private async Task UpdateRevokedCertsCacheContinuouslyAsync()
        {
            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(this.cacheUpdateInterval, this.cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                this.UpdateRevokedCertsCache();
            }
        }

        public void Dispose()
        {
            this.kvRepo?.SaveValueJson(kvRepoKey, this.revokedCertsCache);
            this.cancellationTokenSource.Cancel();
            this.cacheUpdatingTask?.GetAwaiter().GetResult();
        }
    }
}

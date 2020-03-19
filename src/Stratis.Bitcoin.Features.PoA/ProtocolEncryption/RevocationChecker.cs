using System;
using System.Collections.Generic;
using System.Linq;
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
    internal sealed class RevocationRecord
    {
        internal DateTime LastChecked { get; private set; }
        internal bool LastStatus { get; private set; }
        internal string TransactionSigningPubKeyHash { get; private set; }

        private RevocationRecord() { }

        internal static RevocationRecord Revoked(DateTime lastChecked, string transactionSigningPubKeyHash)
        {
            return new RevocationRecord() { LastChecked = lastChecked, LastStatus = true, TransactionSigningPubKeyHash = transactionSigningPubKeyHash };
        }

        internal static RevocationRecord Valid(DateTime lastChecked, string transactionSigningPubKeyHash = null)
        {
            return new RevocationRecord() { LastChecked = lastChecked, TransactionSigningPubKeyHash = transactionSigningPubKeyHash };
        }

        internal void Update(DateTime lastChecked, bool status)
        {
            this.LastChecked = lastChecked;
            this.LastStatus = status;
        }
    }

    public interface IRevocationChecker : IDisposable
    {
        /// <summary>
        /// As there can be multiple certificates in existence for a given P2PKH address (i.e. e.g. with different expiry dates), we determine revocation via thumbprint.
        /// </summary>
        /// <param name="thumbprint">The thumbprint of the certificate to check the revocation status of.</param>
        /// <param name="allowCached">Indicate whether it is acceptable to use the checker's local cache of the status, or force a check with the CA.</param>
        /// <returns><c>true</c> if the given certificate has been revoked.</returns>
        bool IsCertificateRevoked(string thumbprint, bool allowCached = true);

        /// <summary>
        /// Tries to determine if a certificate is revoked by checking the transaction signing key of the node that signed the certificate.
        /// </summary>
        /// <param name="base64PubKeyHash">This is usually the node's transaction signing key in base64 form.</param>
        /// <returns><c>True</c> id the status is not revokek, otherwise false (even if Certificate Authority is uncontactable.</returns>
        bool IsCertificateRevokedByTransactionSigningKeyHash(string base64PubKeyHash);

        void Initialize();
    }

    public sealed class RevocationChecker : IRevocationChecker
    {
        private const string kvRepoKey = "revokedcerts";

        private readonly IKeyValueRepository kvRepo;

        private readonly ILogger logger;

        private readonly IDateTimeProvider dateTimeProvider;

        private readonly TextFileConfiguration configuration;

        private Dictionary<string, RevocationRecord> revokedCertsCache;

        private CaClient client;

        private Task cacheUpdatingTask;

        private readonly CancellationTokenSource cancellationTokenSource;

        private readonly TimeSpan cacheUpdateInterval = TimeSpan.FromHours(1);

        public RevocationChecker(
            NodeSettings nodeSettings,
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
            var caUrl = this.configuration.GetOrDefault<string>("caurl", "https://localhost:5001");
            var caPassword = this.configuration.GetOrDefault<string>(CertificatesManager.CaPasswordKey, null);
            var caAccountId = this.configuration.GetOrDefault<int>(CertificatesManager.CaAccountIdKey, 0);

            this.client = new CaClient(new Uri(caUrl), new HttpClient(), caAccountId, caPassword);

            this.revokedCertsCache = (this.kvRepo == null) ? null : this.kvRepo.LoadValueJson<Dictionary<string, RevocationRecord>>(kvRepoKey);

            if (this.revokedCertsCache == null)
            {
                this.revokedCertsCache = new Dictionary<string, RevocationRecord>();
                this.UpdateRevokedCertsCache();
            }

            this.cacheUpdatingTask = this.UpdateRevokedCertsCacheContinuouslyAsync();
        }

        /// <inheritdoc/>
        public bool IsCertificateRevoked(string thumbprint, bool allowCached = true)
        {
            RevocationRecord record = null;

            if (allowCached && this.revokedCertsCache.TryGetValue(thumbprint, out record))
            {
                if ((this.dateTimeProvider.GetUtcNow() - record.LastChecked) < this.cacheUpdateInterval)
                    return record.LastStatus;
            }

            if (record == null)
                record = RevocationRecord.Valid(this.dateTimeProvider.GetUtcNow());

            // Cannot use cache, or no record existed yet. Ask CA server directly.
            try
            {
                string status = this.client.GetCertificateStatus(thumbprint);
                record.Update(this.dateTimeProvider.GetUtcNow(), status != "1");
            }
            catch (Exception e)
            {
                this.logger.LogDebug("Error while checking certificate status: '{0}'.", e.ToString());
            }

            this.revokedCertsCache[thumbprint] = record;

            return record.LastStatus;
        }

        /// <inheritdoc/>
        public bool IsCertificateRevokedByTransactionSigningKeyHash(string base64PubKeyHash)
        {
            KeyValuePair<string, RevocationRecord> fromCache = this.revokedCertsCache.FirstOrDefault(c => c.Value.TransactionSigningPubKeyHash == base64PubKeyHash);

            // If we can't get the value from the cache, check the CA.
            // If the CA is down or we cannot determine the status for whatever reason then reject the certificate is regarded as revoked or invalid.
            if (fromCache.Value == null)
            {
                try
                {
                    CertificateInfoModel certificateInfo = this.client.GetCertificateForTransactionSigningPubKeyHash(base64PubKeyHash);
                    return certificateInfo.Status != CertificateStatus.Good;
                }
                catch (Exception e)
                {
                    this.logger.LogDebug("Returning certificate as revoked as the there was an error whilst checking the certificate status on the CA and/or the node's certificate is unknown: '{0}'.", e.ToString());
                    return true;
                }
            }

            return fromCache.Value.LastStatus;
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
                        this.revokedCertsCache[certificateInfoModel.Thumbprint] = RevocationRecord.Valid(this.dateTimeProvider.GetUtcNow(), Convert.ToBase64String(certificateInfoModel.TransactionSigningPubKeyHash));
                    else
                        this.revokedCertsCache[certificateInfoModel.Thumbprint] = RevocationRecord.Revoked(this.dateTimeProvider.GetUtcNow(), Convert.ToBase64String(certificateInfoModel.TransactionSigningPubKeyHash));
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

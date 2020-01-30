using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CertificateAuthority.Controllers;
using CertificateAuthority.Models;
using NBitcoin;
using Newtonsoft.Json;

namespace CertificateAuthority
{
    public class CaClient
    {
        private const string InitializeCertificateAuthorityEndpoint = "api/certificates/initialize_ca";
        private const string GetCaCertificateEndpoint = "api/certificates/get_ca_certificate";
        private const string GetAllCertificatesEndpoint = "api/certificates/get_all_certificates";
        private const string GetRevokedCertificatesEndpoint = "api/certificates/get_revoked_certificates";
        private const string GetCertificateForPubKeyHashEndpoint = "api/certificates/get_certificate_for_pubkey_hash";
        private const string GetCertificateStatusEndpoint = "api/certificates/get_certificate_status";
        private const string GenerateCertificateSigningRequestEndpoint = "api/certificates/generate_certificate_signing_request";
        private const string IssueCertificateEndpoint = "api/certificates/issue_certificate_using_request_string";
        private const string GetCertificatePublicKeysEndpoint = "api/certificates/get_certificate_public_keys";

        private const string CreateAccountEndpoint = "api/accounts/create_account";

        private const string JsonContentType = "application/json";
        private readonly Uri baseApiUrl;
        private readonly HttpClient httpClient;

        private readonly int accountId;
        private readonly string password;

        public CaClient(Uri baseApiUrl, HttpClient httpClient)
        {
            this.baseApiUrl = baseApiUrl;
            this.httpClient = httpClient;
            this.httpClient.DefaultRequestHeaders.Accept.Clear();
            this.httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonContentType));
        }

        public CaClient(Uri baseApiUrl, HttpClient httpClient, int accountId, string password) : this(baseApiUrl, httpClient)
        {
            this.accountId = accountId;
            this.password = password;
        }

        public bool InitializeCertificateAuthority(string mnemonic, string mnemonicPassword, Network network)
        {
            // Happy to not use RequestFromCA method for now because this is a more specialised method, might need different logic at some point.

            var mnemonicModel = new InitializeCertificateAuthorityModel(mnemonic, mnemonicPassword, network.Consensus.CoinType, network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS][0], this.password);

            HttpResponseMessage response = this.httpClient.PostAsJsonAsync($"{this.baseApiUrl}{InitializeCertificateAuthorityEndpoint}", mnemonicModel).GetAwaiter().GetResult();

            string responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            bool initialized = JsonConvert.DeserializeObject<bool>(responseString);

            return initialized;
        }

        public CertificateInfoModel GetCaCertificate()
        {
            var credentialsModel = new CredentialsModel()
            {
                AccountId = this.accountId,
                Password = this.password
            };

            return this.RequestFromCA<CertificateInfoModel>(GetCaCertificateEndpoint, credentialsModel);
        }

        public int CreateAccount(string name, string organizationUnit, string organization, string locality, string stateOrProvince, string emailAddress, string country)
        {
            // TODO: Request all permissions by default, or request none and require admin to add them?

            string passHash = DataHelper.ComputeSha256Hash(this.password);

            var createAccountModel = new CreateAccount(name,
                passHash,
                (int)(AccountAccessFlags.IssueCertificates | AccountAccessFlags.AccessAccountInfo | AccountAccessFlags.AccessAnyCertificate),
                organizationUnit,
                organization,
                locality,
                stateOrProvince,
                emailAddress,
                country,
                AccountsController.ValidPermissions,
                this.accountId,
                this.password);


            return this.RequestFromCA<int>(CreateAccountEndpoint, createAccountModel);
        }

        public List<CertificateInfoModel> GetAllCertificates()
        {
            var credentialsModel = new CredentialsModel()
            {
                AccountId = this.accountId,
                Password = this.password
            };

            return this.RequestFromCA<List<CertificateInfoModel>>(GetAllCertificatesEndpoint, credentialsModel);
        }

        public async Task<List<PubKey>> GetCertificatePublicKeysAsync()
        {
            var credentialsModel = new CredentialsModel()
            {
                AccountId = this.accountId,
                Password = this.password
            };

            List<string> pubKeyList = this.RequestFromCA<List<string>>(GetCertificatePublicKeysEndpoint, credentialsModel);

            return pubKeyList.Select(x => new PubKey(x)).ToList();
        }

        public List<string> GetRevokedCertificates()
        {
            var credentialsModel = new CredentialsModel()
            {
                AccountId = this.accountId,
                Password = this.password
            };

            return this.RequestFromCA<List<string>>(GetRevokedCertificatesEndpoint, credentialsModel);
        }

        public CertificateInfoModel GetCertificateForTransactionSigningPubKeyHash(string base64PubKeyHash)
        {
            var pubKeyModel = new CredentialsModelWithPubKeyHashModel()
            {
                AccountId = this.accountId,
                PubKeyHash = base64PubKeyHash,
                Password = this.password
            };

            return this.RequestFromCA<CertificateInfoModel>(GetCertificateForPubKeyHashEndpoint, pubKeyModel);
        }

        public string GetCertificateStatus(string thumbprint)
        {
            var thumbprintModel = new CredentialsModelWithThumbprintModel()
            {
                AccountId = this.accountId,
                Thumbprint = thumbprint,
                Password = this.password
            };

            return this.RequestFromCA<string>(GetCertificateStatusEndpoint, thumbprintModel);
        }

        /// <param name="pubKey">The public key for the P2PKH address, in base64 format.</param>
        /// <param name="address">The P2PKH base58 address string.</param>
        /// <param name="transactionSigningPubKeyHash">The pubkey hash of the transaction signing key.</param>
        /// <param name="blockSigningPubKey">The pubkey of the block signing key.</param>
        public CertificateSigningRequestModel GenerateCertificateSigningRequest(string pubKey, string address, string transactionSigningPubKeyHash, string blockSigningPubKey)
        {
            var generateCsrModel = new GenerateCertificateSigningRequestModel()
            {
                AccountId = this.accountId,
                Address = address,
                Password = this.password,
                PubKey = pubKey,
                TransactionSigningPubKeyHash = transactionSigningPubKeyHash,
                BlockSigningPubKey = blockSigningPubKey
            };

            return this.RequestFromCA<CertificateSigningRequestModel>(GenerateCertificateSigningRequestEndpoint, generateCsrModel);
        }

        public CertificateInfoModel IssueCertificate(string signedCsr)
        {
            var issueCertModel = new IssueCertificateFromFileContentsModel()
            {
                AccountId = this.accountId,
                CertificateRequestFileContents = signedCsr,
                Password = this.password
            };

            return this.RequestFromCA<CertificateInfoModel>(IssueCertificateEndpoint, issueCertModel);
        }

        /// <summary>
        /// We use this method because it has some semblance of error handling.
        /// Avoids errors that otherwise look like serialization problems (i.e. when response.Content is not the json object we are expecting)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="endpoint"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        private T RequestFromCA<T>(string endpoint, object model)
        {
            HttpResponseMessage response = this.httpClient.PostAsJsonAsync($"{this.baseApiUrl}{endpoint}", model).GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
            {
                string errorMessage = $"Failed to connect to the CA. Response Code: {response.StatusCode}.";
                if (response.Content != null)
                {
                    errorMessage += $" Message: {response.Content.ReadAsStringAsync().GetAwaiter().GetResult()}";
                }

                throw new Exception(errorMessage);
            }

            string responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonConvert.DeserializeObject<T>(responseString);
        }
    }
}

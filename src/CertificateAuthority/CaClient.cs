using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
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
        private const string GetCertificateForAddressEndpoint = "api/certificates/get_certificate_for_address";
        private const string GetCertificateForPubKeyHashEndpoint = "api/certificates/get_certificate_for_pubkey_hash";
        private const string GetCertificateStatusEndpoint = "api/certificates/get_certificate_status";
        private const string GenerateCertificateSigningRequestEndpoint = "api/certificates/generate_certificate_signing_request";
        private const string IssueCertificateEndpoint = "api/certificates/issue_certificate_using_request_string";
        private const string GetCertificatePublicKeysEndpoint = "api/certificates/get_certificate_public_keys";

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
            var mnemonicModel = new CredentialsModelWithMnemonicModel(mnemonic, mnemonicPassword, network.Consensus.CoinType, network.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS][0], this.accountId, this.password);

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

            HttpResponseMessage response = this.httpClient.PostAsJsonAsync($"{this.baseApiUrl}{GetCaCertificateEndpoint}", credentialsModel).GetAwaiter().GetResult();

            string responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            CertificateInfoModel caCert = JsonConvert.DeserializeObject<CertificateInfoModel>(responseString);

            return caCert;
        }

        public List<CertificateInfoModel> GetAllCertificates()
        {
            var credentialsModel = new CredentialsModel()
            {
                AccountId = this.accountId,
                Password = this.password
            };

            HttpResponseMessage response = this.httpClient.PostAsJsonAsync($"{this.baseApiUrl}{GetAllCertificatesEndpoint}", credentialsModel).GetAwaiter().GetResult();

            string responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            List<CertificateInfoModel> certList = JsonConvert.DeserializeObject<List<CertificateInfoModel>>(responseString);

            return certList;
        }

        public List<PubKey> GetCertificatePublicKeys()
        {
            var credentialsModel = new CredentialsModel()
            {
                AccountId = this.accountId,
                Password = this.password
            };

            HttpResponseMessage response = this.httpClient.PostAsJsonAsync($"{this.baseApiUrl}{GetCertificatePublicKeysEndpoint}", credentialsModel).GetAwaiter().GetResult();

            string responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            List<PubKey> pubKeyList = JsonConvert.DeserializeObject<List<PubKey>>(responseString);

            return pubKeyList;
        }

        public List<string> GetRevokedCertificates()
        {
            var credentialsModel = new CredentialsModel()
            {
                AccountId = this.accountId,
                Password = this.password
            };

            HttpResponseMessage response = this.httpClient.PostAsJsonAsync($"{this.baseApiUrl}{GetRevokedCertificatesEndpoint}", credentialsModel).GetAwaiter().GetResult();

            string responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            List<string> revokedCertList = JsonConvert.DeserializeObject<List<string>>(responseString);

            return revokedCertList;
        }

        public CertificateInfoModel GetCertificateForAddress(string address)
        {
            var addressModel = new CredentialsModelWithAddressModel()
            {
                AccountId = this.accountId,
                Address = address,
                Password = this.password
            };

            HttpResponseMessage response = this.httpClient.PostAsJsonAsync($"{this.baseApiUrl}{GetCertificateForAddressEndpoint}", addressModel).GetAwaiter().GetResult();

            string responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            CertificateInfoModel cert = JsonConvert.DeserializeObject<CertificateInfoModel>(responseString);

            return cert;
        }

        public CertificateInfoModel GetCertificateForPubKeyHash(string pubKeyHash)
        {
            var pubKeyModel = new CredentialsModelWithPubKeyHashModel()
            {
                AccountId = this.accountId,
                PubKeyHash = pubKeyHash,
                Password = this.password
            };

            HttpResponseMessage response = this.httpClient.PostAsJsonAsync($"{this.baseApiUrl}{GetCertificateForPubKeyHashEndpoint}", pubKeyModel).GetAwaiter().GetResult();

            string responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            CertificateInfoModel cert = JsonConvert.DeserializeObject<CertificateInfoModel>(responseString);

            return cert;
        }

        public string GetCertificateStatus(string thumbprint)
        {
            var thumbprintModel = new CredentialsModelWithThumbprintModel()
            {
                AccountId = this.accountId,
                Thumbprint = thumbprint,
                Password = this.password
            };

            HttpResponseMessage response = this.httpClient.PostAsJsonAsync($"{this.baseApiUrl}{GetCertificateStatusEndpoint}", thumbprintModel).GetAwaiter().GetResult();

            string responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            string status = JsonConvert.DeserializeObject<string>(responseString);

            return status;
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

            HttpResponseMessage response = this.httpClient.PostAsJsonAsync($"{this.baseApiUrl}{GenerateCertificateSigningRequestEndpoint}", generateCsrModel).GetAwaiter().GetResult();

            string responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            CertificateSigningRequestModel csrModel = JsonConvert.DeserializeObject<CertificateSigningRequestModel>(responseString);

            return csrModel;
        }

        public CertificateInfoModel IssueCertificate(string signedCsr)
        {
            var issueCertModel = new IssueCertificateFromFileContentsModel()
            {
                AccountId = this.accountId,
                CertificateRequestFileContents = signedCsr,
                Password = this.password
            };

            HttpResponseMessage response = this.httpClient.PostAsJsonAsync($"{this.baseApiUrl}{IssueCertificateEndpoint}", issueCertModel).GetAwaiter().GetResult();

            string responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            CertificateInfoModel certificateInfoModel = JsonConvert.DeserializeObject<CertificateInfoModel>(responseString);

            return certificateInfoModel;
        }
    }
}

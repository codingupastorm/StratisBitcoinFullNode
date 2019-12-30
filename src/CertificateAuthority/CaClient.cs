using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using CertificateAuthority.Models;
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
        private const string GetCertificateStatusEndpoint = "api/certificates/get_certificate_status";
        private const string GenerateCertificateSigningRequestEndpoint = "api/certificates/generate_certificate_signing_request";
        private const string IssueCertificateEndpoint = "api/certificates/issue_certificate_using_request_string";
        
        private const string JsonContentType = "application/json";
        private Uri baseApiUrl;
        private HttpClient httpClient;

        private int accountId;
        private string password;

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

        public bool InitializeCertificateAuthority(string mnemonic, string mnemonicPassword)
        {
            var mnemonicModel = new CredentialsModelWithMnemonicModel(mnemonic, mnemonicPassword, this.accountId, this.password);
            
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
        /// <returns></returns>
        public CertificateSigningRequestModel GenerateCertificateSigningRequest(string pubKey, string address)
        {
            var generateCsrModel = new GenerateCertificateSigningRequestModel()
            {
                AccountId = this.accountId,
                Address = address,
                Password = this.password,
                PubKey = pubKey
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

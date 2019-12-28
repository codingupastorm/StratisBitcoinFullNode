using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using CertificateAuthority.API;
using CertificateAuthority.Models;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NBitcoin;
using Org.BouncyCastle.Pkcs;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace CertificateAuthority.Tests.FullProjectTests
{
    public class IntegrationTests
    {
        private const int TestAccountId = 1;
        private const string TestPassword = "4815162342";
        private const string CaMnemonic = "young shoe immense usual faculty edge habit misery swarm tape viable toddler";
        private const string CaMnemonicPassword = "node";

        private readonly Network network;

        public IntegrationTests()
        {
            this.network = new StratisRegTest();

            // TODO: Make a clean data folder for each CA startup, and specify it in the CA settings.
        }
        
        [Fact]
        public async Task CertificateAuthorityTestServerStartsUpAsync()
        {
            IWebHostBuilder builder = WebHost.CreateDefaultBuilder();
            builder.UseStartup<Startup>();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), TestAccountId, TestPassword);

            List<CertificateInfoModel> response = client.GetAllCertificates();

            Assert.NotNull(response);

            server.Dispose();
        }

        [Fact]
        public async Task CertificateAuthorityTestServerGetsInitializedAsync()
        {
            IWebHostBuilder builder = WebHost.CreateDefaultBuilder();
            builder.UseStartup<Startup>();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), TestAccountId, TestPassword);

            Assert.True(client.InitializeCertificateAuthority(CaMnemonic, CaMnemonicPassword));
            
            server.Dispose();
        }

        [Fact]
        public async Task CertificateAuthorityCanGenerateCertificateSigningRequestAsync()
        {
            IWebHostBuilder builder = WebHost.CreateDefaultBuilder();
            builder.UseStartup<Startup>();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), TestAccountId, TestPassword);

            Assert.True(client.InitializeCertificateAuthority(CaMnemonic, CaMnemonicPassword));

            var privateKey = new Key();
            PubKey pubKey = privateKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(this.network);

            CertificateSigningRequestModel response = client.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString());

            Assert.NotNull(response);
            Assert.NotEmpty(response.CertificateSigningRequestContent);

            byte[] csrTemp = Convert.FromBase64String(response.CertificateSigningRequestContent);
            var unsignedCsr = new Pkcs10CertificationRequestDelaySigned(csrTemp);

            Assert.NotNull(unsignedCsr);

            server.Dispose();
        }

        [Fact]
        public async Task CertificateAuthorityCanIssueCertificateAsync()
        {
            IWebHostBuilder builder = WebHost.CreateDefaultBuilder();
            builder.UseStartup<Startup>();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), TestAccountId, TestPassword);

            Assert.True(client.InitializeCertificateAuthority(CaMnemonic, CaMnemonicPassword));

            var privateKey = new Key();

            PubKey pubKey = privateKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(this.network);

            CertificateSigningRequestModel response = client.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString());

            string signedCsr = CaCertificatesManager.SignCertificateSigningRequest(response.CertificateSigningRequestContent, privateKey);

            CertificateInfoModel certInfo = client.IssueCertificate(signedCsr);

            Assert.NotNull(certInfo);
            Assert.Equal(address.ToString(), certInfo.Address);

            var certificate = new X509Certificate(Convert.FromBase64String(certInfo.CertificateContentDer));

            Assert.NotNull(certificate);

            server.Dispose();
        }
    }
}

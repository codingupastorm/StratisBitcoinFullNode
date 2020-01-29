using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using CertificateAuthority.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using NBitcoin;
using Org.BouncyCastle.Pkcs;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace CertificateAuthority.Tests.FullProjectTests
{
    public sealed class CertificateAuthorityIntegrationTests
    {
        private readonly Network network;

        public CertificateAuthorityIntegrationTests()
        {
            this.network = new StratisRegTest();
        }

        [Fact]
        public void CertificateAuthorityTestServerStartsUp()
        {
            IWebHostBuilder builder = CaTestHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), Settings.AdminAccountId, CaTestHelper.AdminPassword);

            CaTestHelper.InitializeCa(server);

            List<CertificateInfoModel> response = client.GetAllCertificates();

            Assert.NotNull(response);

            server.Dispose();
        }

        [Fact]
        public void CertificateAuthorityTestServerGetsInitialized()
        {
            IWebHostBuilder builder = CaTestHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), Settings.AdminAccountId, CaTestHelper.AdminPassword);

            Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

            server.Dispose();
        }

        [Fact]
        public void CertificateAuthorityCanGenerateCertificateSigningRequest()
        {
            IWebHostBuilder builder = CaTestHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), Settings.AdminAccountId, CaTestHelper.AdminPassword);

            Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

            var privateKey = new Key();
            PubKey pubKey = privateKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(this.network);

            CertificateSigningRequestModel response = client.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString(), Convert.ToBase64String(pubKey.Hash.ToBytes()), Convert.ToBase64String(pubKey.ToBytes()));

            Assert.NotNull(response);
            Assert.NotEmpty(response.CertificateSigningRequestContent);

            byte[] csrTemp = Convert.FromBase64String(response.CertificateSigningRequestContent);
            var unsignedCsr = new Pkcs10CertificationRequestDelaySigned(csrTemp);

            Assert.NotNull(unsignedCsr);

            server.Dispose();
        }

        [Fact]
        public void CertificateAuthorityCanIssueCertificate()
        {
            IWebHostBuilder builder = CaTestHelper.CreateWebHostBuilder();

            var server = new TestServer(builder);
            var client = new CaClient(server.BaseAddress, server.CreateClient(), Settings.AdminAccountId, CaTestHelper.AdminPassword);

            Assert.True(client.InitializeCertificateAuthority(CaTestHelper.CaMnemonic, CaTestHelper.CaMnemonicPassword, this.network));

            var privateKey = new Key();

            PubKey pubKey = privateKey.PubKey;
            BitcoinPubKeyAddress address = pubKey.GetAddress(this.network);

            CertificateSigningRequestModel response = client.GenerateCertificateSigningRequest(Convert.ToBase64String(pubKey.ToBytes()), address.ToString(), Convert.ToBase64String(pubKey.Hash.ToBytes()), Convert.ToBase64String(pubKey.ToBytes()));

            string signedCsr = CaCertificatesManager.SignCertificateSigningRequest(response.CertificateSigningRequestContent, privateKey);

            CertificateInfoModel certInfo = client.IssueCertificate(signedCsr);

            Assert.NotNull(certInfo);
            Assert.Equal(address.ToString(), certInfo.Address);

            var certificate = new X509Certificate(certInfo.CertificateContentDer);

            Assert.NotNull(certificate);

            //Check that it's in the list of all certificates
            List<CertificateInfoModel> allCerts = client.GetAllCertificates();

            Assert.Single(allCerts);
            Assert.Equal(address.ToString(), allCerts.First().Address);

            CertificateInfoModel queryByAddress = client.GetCertificateForAddress(address.ToString());
            Assert.NotNull(queryByAddress.CertificateContentDer);

            server.Dispose();
        }
    }
}

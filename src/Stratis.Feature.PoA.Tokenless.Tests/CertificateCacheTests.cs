using System.Security.Cryptography.X509Certificates;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class CertificateCacheTests
    {
        private readonly CertificateCache certificateCache;

        public CertificateCacheTests()
        {
            var dataFolder = new DataFolder(TestBase.CreateTestDir(this));
            this.certificateCache = new CertificateCache(dataFolder);
        }

        [Fact]
        public void CanSetAndGetCertificate()
        {
            uint160 testAddress = new uint160(123456);
            var cert = new X509Certificate2("Certificates/cert.crt");

            this.certificateCache.SetCertificate(testAddress, cert);

            X509Certificate2 returnCert = this.certificateCache.GetCertificate(testAddress);

            Assert.NotNull(returnCert);

            // It's a different reference so the objects are different, but the values are identical.
            Assert.Equal(cert.FriendlyName, returnCert.FriendlyName);
            Assert.Equal(cert.PublicKey.Oid.Value, returnCert.PublicKey.Oid.Value);
            Assert.Equal(cert.IssuerName.Name, returnCert.IssuerName.Name);
        }
    }
}

using System.IO;
using System.Security.Cryptography.X509Certificates;
using CertificateAuthority;
using NBitcoin;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common;
using Xunit;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

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
            var testAddress = new uint160(123456);
            var certParser = new X509CertificateParser();
            X509Certificate cert = certParser.ReadCertificate(File.ReadAllBytes("Certificates/cert.crt"));

            this.certificateCache.SetCertificate(testAddress, cert);

            X509Certificate2 cert1 = CaCertificatesManager.ConvertCertificate(cert, new SecureRandom());
            X509Certificate2 returnCert = CaCertificatesManager.ConvertCertificate(this.certificateCache.GetCertificate(testAddress), new SecureRandom());

            Assert.NotNull(returnCert);

            // It's a different reference so the objects are different, but the values are identical.
            Assert.Equal(cert1.FriendlyName, returnCert.FriendlyName);
            Assert.Equal(cert1.PublicKey.Oid.Value, returnCert.PublicKey.Oid.Value);
            Assert.Equal(cert1.IssuerName.Name, returnCert.IssuerName.Name);
        }
    }
}

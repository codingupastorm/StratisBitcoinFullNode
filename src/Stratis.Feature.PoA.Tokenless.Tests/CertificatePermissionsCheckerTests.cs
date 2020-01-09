using System.IO;
using Org.BouncyCastle.X509;
using Xunit;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class CertificatePermissionsCheckerTests
    {
        [Fact]
        public void CheckCertificateHasPermissionFails()
        {
            var certParser = new X509CertificateParser();
            X509Certificate cert = certParser.ReadCertificate(File.ReadAllBytes("Certificates/cert.crt"));

            Assert.False(CertificatePermissionsChecker.ValidateCertificateHasPermission(cert));
            Assert.False(CertificatePermissionsChecker.ValidateCertificateHasPermission(null));
        }
    }
}

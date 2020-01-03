using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class CertificatePermissionsCheckerTests
    {
        [Fact]
        public void CheckCertificateHasPermissionFails()
        {
            var cert = new X509Certificate2("Certificates/cert.crt");
            Assert.False(CertificatePermissionsChecker.ValidateCertificateHasPermission(cert));
            Assert.False(CertificatePermissionsChecker.ValidateCertificateHasPermission(null));
        }
    }
}

using System.IO;
using HashLib;
using MembershipServices;
using Moq;
using NBitcoin;
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

            Assert.False(CertificatePermissionsChecker.ValidateCertificateHasPermission(cert, TransactionSendingPermission.Send));
            Assert.False(CertificatePermissionsChecker.ValidateCertificateHasPermission(null, TransactionSendingPermission.Send));
        }

        [Fact]
        public void CheckSignature()
        {
            var transactionSigningKey = new Key();
            
            var mock = new Mock<IMembershipServicesDirectory>();
            mock.Setup(m => m.GetCertificateForThumbprint(It.IsAny<string>())).Returns(new Mock<X509Certificate>().Object);
            mock.Setup(m => m.ExtractCertificateExtensionFromOid(It.IsAny<X509Certificate>(), It.IsAny<string>())).Returns(transactionSigningKey.PubKey.Hash.ToBytes());

            var checker = new CertificatePermissionsChecker(mock.Object, new ChannelAccessValidator());

            var data = RandomUtils.GetBytes(128);

            var hash = new uint256(HashFactory.Crypto.SHA3.CreateKeccak256().ComputeBytes(data).GetBytes());

            var signature = transactionSigningKey.Sign(hash);

            Assert.True(checker.CheckSignature("mockThumbprint", signature, transactionSigningKey.PubKey, hash));
        }
    }
}

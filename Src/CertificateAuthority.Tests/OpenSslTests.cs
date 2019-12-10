using System.Text;
using OpenSSL.Core;
using OpenSSL.Crypto;
using OpenSSL.Crypto.EC;
using Xunit;

namespace CertificateAuthority.Tests
{
    /// <summary>
    /// Sanity tests for the referenced OpenSSL wrapper module.
    /// This is also intended as a rudimentary form of documentation for usage of the wrapper, as it effectively has none.
    /// </summary>
    public class OpenSslTests
    {
        /// <summary>
        /// The wrapper does not define a shortcut for this particular curve, although it is able to use it.
        /// Therefore we must reference the curve by its internal OpenSSL ID.
        /// </summary>
        private readonly Asn1Object secp256k1 = new Asn1Object(714);

        [Fact]
        public void CanGenerateEllipticCurvePrivateKey()
        {
            // An elliptic curve private key.
            var key = Key.FromCurveName(secp256k1);

            // A key needs to be generated before it can be used.
            // (In reality the key would be retrieved from elsewhere, such as the filesystem)
            key.GenerateKey();

            // Most other methods in the wrapper do not take raw private keys, they are further wrapped in a CryptoKey.
            var cryptoKey = new CryptoKey(key);

            Assert.NotNull(cryptoKey);

            cryptoKey.Dispose();
            key.Dispose();
        }

        [Fact]
        public void CanExportEllipticCurvePrivateKey()
        {
            var key = Key.FromCurveName(secp256k1);
            key.GenerateKey();
            var cryptoKey = new CryptoKey(key);

            // 'BIO' is essentially the OpenSSL equivalent of a stream.
            var bio = BIO.MemoryBuffer();

            // Don't really care how the PKCS#8 output is encrypted in this case.
            // However, using Cipher.Null causes an OpenSSL error, so choose an arbitrary 'real' cipher.
            var cipher = Cipher.AES_128_CBC;

            // Similarly, an empty password is causing an OpenSSL error. So set it to some non-empty string.
            cryptoKey.WritePrivateKey(bio, cipher, "password");
            var bioBytes = bio.ReadBytes((int)bio.BytesPending);

            byte[] privKey = bioBytes.ToArray();

            // This will be a PKCS#8 base64 string.
            string privKeyString = Encoding.ASCII.GetString(privKey);

            Assert.Contains("BEGIN ENCRYPTED PRIVATE KEY", privKeyString);

            bio.Dispose();
            cryptoKey.Dispose();
            key.Dispose();
        }

        [Fact]
        public void CanSignWithEllipticCurvePrivateKey()
        {
            var key = Key.FromCurveName(secp256k1);
            key.GenerateKey();
            var cryptoKey = new CryptoKey(key);

            byte[] dataToSign = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

            DSASignature signature = key.Sign(dataToSign);

            Assert.True(key.Verify(dataToSign, signature));

            cryptoKey.Dispose();
            key.Dispose();
        }
    }
}

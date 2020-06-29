using System;
using System.Security.Cryptography;
using NBitcoin;
using NBitcoin.DataEncoders;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;

namespace CertificateAuthority
{
    public class HDWalletAddressSpace
    {
        private readonly string mnemonicWords;
        private readonly string password;

        public HDWalletAddressSpace(string mnemonicWords, string password)
        {
            this.mnemonicWords = mnemonicWords;
            this.password = password;
        }

        public ExtKey GetKey(string hdPath)
        {
            // Derive HD wallet seed
            var mnemonic = new Mnemonic(this.mnemonicWords);
            ExtKey extendedKey = mnemonic.DeriveExtKey(this.password);
            byte[] chainCode = extendedKey.ChainCode;

            var seedExtKey = new ExtKey(extendedKey.PrivateKey, chainCode);

            // Generate key
            var keyPath = new KeyPath(hdPath);
            ExtKey addressKey = seedExtKey.Derive(keyPath);

            return addressKey;
        }

        public static string GetAddress(byte[] publicKey, byte addressPrefix)
        {
            // Hash public key with SHA256
            byte[] publicKeyHash = new SHA256CryptoServiceProvider().ComputeHash(publicKey);

            // Hash the result of above with RIPEMD160
            var digest = new RipeMD160Digest();
            digest.BlockUpdate(publicKeyHash, 0, publicKeyHash.Length);
            byte[] outArray = new byte[20];
            digest.DoFinal(outArray, 0);

            // Add network prefix
            byte[] extendedRipeMD160 = new byte[outArray.Length + 1];
            extendedRipeMD160[0] = addressPrefix;
            Array.Copy(outArray, 0, extendedRipeMD160, 1, extendedRipeMD160.Length - 1);

            // Base58Check encode
            var e = new Base58CheckEncoder();

            return e.EncodeData(extendedRipeMD160);
        }

        public AsymmetricCipherKeyPair GetCertificateKeyPair(string hdPath, string ecdsaCurveFriendlyName = "secp256k1")
        {
            ExtKey addressKey = GetKey(hdPath);
            var privateKey = new BigInteger(1, addressKey.PrivateKey.GetBytes());

            // Set curve parameters
            X9ECParameters ecdsaCurve = ECNamedCurveTable.GetByName(ecdsaCurveFriendlyName);
            var ecdsaDomainParams = new ECDomainParameters(ecdsaCurve.Curve, ecdsaCurve.G, ecdsaCurve.N, ecdsaCurve.H, ecdsaCurve.GetSeed());

            var q = new X9ECPoint(ecdsaCurve.Curve, addressKey.PrivateKey.PubKey.ToBytes());

            // Create private/public keypair parameters
            var privateParameter = new ECPrivateKeyParameters(privateKey, ecdsaDomainParams);
            //ECPoint q = ecdsaDomainParams.G.Multiply(privateKey);
            var publicParameter = new ECPublicKeyParameters(q.Point, ecdsaDomainParams);

            // Return keypair
            var keyPair = new AsymmetricCipherKeyPair(publicParameter, privateParameter);

            return keyPair;
        }
    }
}

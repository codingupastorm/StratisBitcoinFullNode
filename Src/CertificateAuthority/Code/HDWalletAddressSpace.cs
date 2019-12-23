using System;
using System.Security.Cryptography;
using NBitcoin;
using NBitcoin.DataEncoders;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using ECPoint = Org.BouncyCastle.Math.EC.ECPoint;

namespace CertificateAuthority.Code
{
    public class HDWalletAddressSpace
    {
        private string mnemonicWords;
        private string password;

        public HDWalletAddressSpace(string mnemonicWords, string password)
        {
            this.mnemonicWords = mnemonicWords;
            this.password = password;
        }

        public ExtKey GetKey(string hdPath)
        {
            //Derive HD wallet seed
            Mnemonic mnemonic = new Mnemonic(mnemonicWords);
            ExtKey extendedKey = mnemonic.DeriveExtKey(password);
            byte[] chainCode = extendedKey.ChainCode;

            ExtKey seedExtKey = new ExtKey(extendedKey.PrivateKey, chainCode);

            //Generate key
            KeyPath keyPath = new KeyPath(hdPath);
            ExtKey addressKey = seedExtKey.Derive(keyPath);

            return addressKey;
        }

        public static string GetAddress(byte[] publicKey, byte addressPrefix)
        {
            //Hash public key with SHA256
            byte[] publicKeyHash = new SHA256CryptoServiceProvider().ComputeHash(publicKey);

            //Hash the result of above with RIPEMD160
            RipeMD160Digest digest = new RipeMD160Digest();
            digest.BlockUpdate(publicKeyHash, 0, publicKeyHash.Length);
            byte[] outArray = new byte[20];
            digest.DoFinal(outArray, 0);

            //Add network prefix
            byte[] extendedRipeMD160 = new byte[outArray.Length + 1];
            extendedRipeMD160[0] = addressPrefix;
            Array.Copy(outArray, 0, extendedRipeMD160, 1, extendedRipeMD160.Length - 1);

            //Base58Check encode
            Base58CheckEncoder e = new Base58CheckEncoder();

            return e.EncodeData(extendedRipeMD160);
        }

        public AsymmetricCipherKeyPair GetCertificateKeyPair(string hdPath, string ecdsaCurveFriendlyName = "secp256k1")
        {
            ExtKey addressKey = GetKey(hdPath);
            BigInteger privateKey = new BigInteger(addressKey.PrivateKey.ToBytes());

            // Set curve parameters
            X9ECParameters ecdsaCurve = ECNamedCurveTable.GetByName(ecdsaCurveFriendlyName);
            ECDomainParameters ecdsaDomainParams = new ECDomainParameters(ecdsaCurve.Curve, ecdsaCurve.G, ecdsaCurve.N, ecdsaCurve.H, ecdsaCurve.GetSeed());

            X9ECPoint q = new X9ECPoint(ecdsaCurve.Curve, addressKey.PrivateKey.PubKey.ToBytes());

            // Create private/public key pair parameters
            var privateParameter = new ECPrivateKeyParameters(privateKey, ecdsaDomainParams);
            //ECPoint q = ecdsaDomainParams.G.Multiply(privateKey);
            var publicParameter = new ECPublicKeyParameters(q.Point, ecdsaDomainParams);

            //Return key pair
            AsymmetricCipherKeyPair keyPair = new AsymmetricCipherKeyPair(publicParameter, privateParameter);

            return keyPair;
        }

        public class EcdsaKey
        {
            public string ChainCode { get; set; }

            public string SeedPrivateKey { get; set; }
            
            public string SeedPublicKey { get; set; }

            public string HdPath { get; set; }

            public string Address { get; set; }
            
            public string PublicKeyCompressed { get; set; }
            
            public string PublicKey { get; set; }
            
            public string PrivateKey { get; set; }
        }

        public static EcdsaKey GetKeyInfo(string mnemonicWords, string password, string hdPath, byte addressPrefix)
        {
            //Derive HD wallet seed
            Mnemonic mnemonic = new Mnemonic(mnemonicWords);
            ExtKey extendedKey = mnemonic.DeriveExtKey(password);
            byte[] chainCode = extendedKey.ChainCode;

            ExtKey seedExtKey = new ExtKey(extendedKey.PrivateKey, chainCode);

            //Generate key
            KeyPath keyPath = new KeyPath(hdPath);
            ExtKey addressKey = seedExtKey.Derive(keyPath);

            EcdsaKey key = new EcdsaKey
            {
                ChainCode = BitConverter.ToString(chainCode).Replace("-", string.Empty),

                SeedPrivateKey = seedExtKey.PrivateKey.ToHex(),
                SeedPublicKey = seedExtKey.PrivateKey.PubKey.ToHex(),

                HdPath = keyPath.ToString(),
                Address = GetAddress(addressKey.PrivateKey.PubKey.ToBytes(false), addressPrefix),
                PublicKeyCompressed = addressKey.PrivateKey.PubKey.ToHex(),
                PublicKey = addressKey.PrivateKey.PubKey.Decompress().ToHex(),
                PrivateKey = addressKey.PrivateKey.ToHex(),
            };

            return key;
        }
    }
}

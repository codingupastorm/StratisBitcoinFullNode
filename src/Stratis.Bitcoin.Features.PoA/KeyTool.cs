using System.IO;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Bitcoin.Features.PoA
{
    public sealed class KeyTool
    {
        public const string BlockSigningKeyFileName = "federationKey.dat";
        public const string TransactionSigningKeyFileName = "transactionSigning.dat";

        private readonly string path;

        public KeyTool(DataFolder dataFolder)
        {
            this.path = dataFolder.RootPath;
        }

        public KeyTool(string path)
        {
            this.path = path;
        }

        /// <summary>Generates a new private key.</summary>
        /// <returns>The generated private <see cref="Key"/>.</returns>
        public Key GeneratePrivateKey()
        {
            var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            Key privateKey = mnemonic.DeriveExtKey().PrivateKey;

            return privateKey;
        }

        /// <summary>Gets the default path for private key saving and loading.</summary>
        /// <param name="keyType">The <see cref="KeyType"/> to get the file path for.</param>
        /// <returns>The path to the federation key file.</returns>
        public string GetKeyFilePath(KeyType keyType)
        {
            string filePath = null;

            switch (keyType)
            {
                case KeyType.FederationKey:
                    filePath = Path.Combine(this.path, BlockSigningKeyFileName);
                    break;
                case KeyType.TransactionSigningKey:
                    filePath = Path.Combine(this.path, TransactionSigningKeyFileName);
                    break;
            }

            return filePath;
        }

        /// <summary>Saves private key to default path.</summary>
        /// <param name="privateKey">The key to be saved.</param>
        /// <param name="keyType">The <see cref="KeyType"/> to be saved.</param>
        public void SavePrivateKey(Key privateKey, KeyType keyType)
        {
            using (var ms = new MemoryStream())
            {
                var stream = new BitcoinStream(ms, true);
                stream.ReadWrite(ref privateKey);

                ms.Seek(0, SeekOrigin.Begin);

                using (FileStream fileStream = File.Create(GetKeyFilePath(keyType)))
                {
                    ms.CopyTo(fileStream);
                }
            }
        }

        /// <summary>Loads the private key from default path.</summary>
        /// <param name="keyType">The <see cref="KeyType"/> to load.</param>
        /// <returns>The loaded <see cref="Key"/>.</returns>
        public Key LoadPrivateKey(KeyType keyType)
        {
            string filePath = GetKeyFilePath(keyType);

            if (!File.Exists(filePath))
                return null;

            using (FileStream readStream = File.OpenRead(filePath))
            {
                var privateKey = new Key();
                var stream = new BitcoinStream(readStream, false);
                stream.ReadWrite(ref privateKey);

                return privateKey;
            }
        }
    }

    public enum KeyType
    {
        FederationKey,
        TransactionSigningKey
    }
}

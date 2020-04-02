using System;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Feature.PoA.Tokenless.KeyStore
{
    public class TokenlessKeyStore
    {
        public string EncryptedSeed { get; set; }

        public string ChainCode { get; set; }

        public string ExtPubKey0 { get; set; }

        public string ExtPubKey1 { get; set; }

        public string ExtPubKey2 { get; set; }

        public TokenlessKeyStore()
        {
        }

        public TokenlessKeyStore(Network network, string password, ref Mnemonic mnemonic)
        {
            Guard.NotEmpty(password, nameof(password));

            // Generate the root seed used to generate keys from a mnemonic picked at random
            // and a passphrase optionally provided by the user.
            mnemonic = mnemonic ?? new Mnemonic(Wordlist.English, WordCount.Twelve);

            ExtKey seedExtKey = GetSeedExtKey(mnemonic);

            this.ExtPubKey0 = GetAccountExtPubKey(network.Consensus.CoinType, seedExtKey, TokenlessKeyStoreAccount.TransactionSigning).ToString(network);
            this.ExtPubKey1 = GetAccountExtPubKey(network.Consensus.CoinType, seedExtKey, TokenlessKeyStoreAccount.BlockSigning).ToString(network);
            this.ExtPubKey2 = GetAccountExtPubKey(network.Consensus.CoinType, seedExtKey, TokenlessKeyStoreAccount.P2PCertificates).ToString(network);

            this.ChainCode = Convert.ToBase64String(seedExtKey.ChainCode);

            this.EncryptedSeed = seedExtKey.PrivateKey.GetEncryptedBitcoinSecret(password, network).ToWif();
        }

        public static ExtKey GetSeedExtKey(Mnemonic mnemonic)
        {
            return mnemonic.DeriveExtKey();
        }

        public static ExtPubKey GetAccountExtPubKey(int coinType, ExtKey seedExtKey, TokenlessKeyStoreAccount tokenlessWalletAccount)
        {
            /*
            - transaction signing (m/44'/105'/0'/0/N) where N is a zero based key ID
            - block signing (m/44'/105'/1'/0/N) where N is a zero based key ID
            - P2P certificates (m/44'/105'/2'/K/N) where N is a zero based key ID
            */
            return seedExtKey.Derive(new KeyPath($"m/44'/{coinType}'/{ (int)tokenlessWalletAccount }'")).Neuter();
        }

        public static PubKey GetPubKey(ExtPubKey accountExtPubKey, int addressIndex, int addressType = 0)
        {
            var keyPath = new KeyPath($"{addressType}/{addressIndex}");
            ExtPubKey extPubKey = accountExtPubKey.Derive(keyPath);
            return extPubKey.PubKey;
        }

        public PubKey GetPubKey(Network network, TokenlessKeyStoreAccount tokenlessWalletAccount, int addressIndex, int addressType = 0)
        {
            return GetPubKey(ExtPubKey.Parse(new[] { this.ExtPubKey0, this.ExtPubKey1, this.ExtPubKey2 }[(int)tokenlessWalletAccount], network), addressIndex, addressType);
        }

        [NoTrace]
        public ExtKey GetSeedExtKey(Network network, string password)
        {
            return new ExtKey(Key.Parse(this.EncryptedSeed, password, network), Convert.FromBase64String(this.ChainCode));
        }

        public static Key GetKey(int coinType, ExtKey seedExtKey, TokenlessKeyStoreAccount tokenlessWalletAccount, int addressIndex, int addressType = 0)
        {
            string hdPath = $"m/44'/{coinType}'/{(int)tokenlessWalletAccount}'/{addressType}/{addressIndex}";
            ExtKey pathExtKey = seedExtKey.Derive(new KeyPath(hdPath));

            return pathExtKey.PrivateKey;
        }

        public Key GetKey(Network network, string password, TokenlessKeyStoreAccount tokenlessWalletAccount, int addressIndex, int addressType = 0)
        {
            return GetKey(network.Consensus.CoinType, this.GetSeedExtKey(network, password), tokenlessWalletAccount, addressIndex, addressType);
        }

        public static Key GetKey(int coinType, Mnemonic mnemonic, TokenlessKeyStoreAccount tokenlessWalletAccount, int addressIndex, int addressType = 0)
        {
            return GetKey(coinType, mnemonic.DeriveExtKey(), tokenlessWalletAccount, addressIndex, addressType);
        }
    }
}

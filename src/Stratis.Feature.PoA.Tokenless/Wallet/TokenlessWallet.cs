using System;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Feature.PoA.Tokenless.Wallet
{
    public class TokenlessWallet
    {
        public string EncryptedSeed { get; set; }

        public string ChainCode { get; set; }

        public string ExtPubKey0 { get; set; }

        public string ExtPubKey1 { get; set; }

        public string ExtPubKey2 { get; set; }

        public TokenlessWallet()
        {
        }

        public TokenlessWallet(Network network, string password, string passphrase, ref Mnemonic mnemonic)
        {
            Guard.NotEmpty(password, nameof(password));

            // Generate the root seed used to generate keys from a mnemonic picked at random
            // and a passphrase optionally provided by the user.
            mnemonic = mnemonic ?? new Mnemonic(Wordlist.English, WordCount.Twelve);

            ExtKey extendedKey = GetExtKey(mnemonic, passphrase);
            ExtKey seedExtKey = GetSeedExtKey(extendedKey);

            this.ExtPubKey0 = GetAccountExtPubKey(network.Consensus.CoinType, seedExtKey, TokenlessWalletAccount.TransactionSigning).ToString(network);
            this.ExtPubKey1 = GetAccountExtPubKey(network.Consensus.CoinType, seedExtKey, TokenlessWalletAccount.BlockSigning).ToString(network);
            this.ExtPubKey2 = GetAccountExtPubKey(network.Consensus.CoinType, seedExtKey, TokenlessWalletAccount.P2PCertificates).ToString(network);

            this.ChainCode = Convert.ToBase64String(extendedKey.ChainCode);

            this.EncryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, network).ToWif();
        }

        public static ExtKey GetExtKey(Mnemonic mnemonic, string passphrase = null)
        {
            return mnemonic.DeriveExtKey(passphrase);
        }

        public static ExtKey GetSeedExtKey(ExtKey extendedKey)
        {
            return new ExtKey(extendedKey.PrivateKey, extendedKey.ChainCode);
        }

        public static ExtPubKey GetAccountExtPubKey(int coinType, ExtKey seedExtKey, TokenlessWalletAccount tokenlessWalletAccount)
        {
            /*
            - transaction signing (m/44'/105'/0'/0/N) where N is a zero based key ID
            - block signing (m/44'/105'/1'/0/N) where N is a zero based key ID
            - P2P certificates (m/44'/105'/2'/K/N) where N is a zero based key ID
            */
            return seedExtKey.Derive(new KeyPath($"m/44'/{coinType}'/{ (int)tokenlessWalletAccount }'")).Neuter();
        }

        public static PubKey GetPubKey(ExtPubKey account, int addressIndex, int addressType = 0)
        {
            var keyPath = new KeyPath($"{addressType}/{addressIndex}");
            ExtPubKey extPubKey = account.Derive(keyPath);
            return extPubKey.PubKey;
        }

        public PubKey GetPubKey(Network network, TokenlessWalletAccount tokenlessWalletAccount, int addressIndex, int addressType = 0)
        {
            return GetPubKey(ExtPubKey.Parse(new[] { this.ExtPubKey0, this.ExtPubKey1, this.ExtPubKey2 }[(int)tokenlessWalletAccount], network), addressIndex, addressType);
        }

        [NoTrace]
        public ExtKey GetExtKey(Network network, string password)
        {
            return new ExtKey(Key.Parse(this.EncryptedSeed, password, network), Convert.FromBase64String(this.ChainCode));
        }

        public ExtKey GetExtKey(Network network, string password, TokenlessWalletAccount tokenlessWalletAccount, int addressIndex, int addressType = 0)
        {
            string hdPath = $"m/44'/{network.Consensus.CoinType}'/{(int)tokenlessWalletAccount}'/{addressType}/{addressIndex}";
            ExtKey seedExtKey = this.GetExtKey(network, password);
            ExtKey pathExtKey = seedExtKey.Derive(new KeyPath(hdPath));

            return pathExtKey;
        }
    }
}

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
            Guard.NotNull(passphrase, nameof(passphrase));

            // Generate the root seed used to generate keys from a mnemonic picked at random
            // and a passphrase optionally provided by the user.
            mnemonic = mnemonic ?? new Mnemonic(Wordlist.English, WordCount.Twelve);

            ExtKey extendedKey = mnemonic.DeriveExtKey(passphrase);

            // Create a wallet file.
            this.EncryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, network).ToWif();
            this.ChainCode = Convert.ToBase64String(extendedKey.ChainCode);

            Key privateKey = Key.Parse(this.EncryptedSeed, password, network);
            var seedExtKey = new ExtKey(privateKey, extendedKey.ChainCode);

            /*
            - transaction signing (m/44'/105'/0'/0/N) where N is a zero based key ID
            - block signing (m/44'/105'/1'/0/N) where N is a zero based key ID
            - P2P certificates (m/44'/105'/2'/K/N) where N is a zero based key ID
            */

            ExtKey addressExtKey0 = seedExtKey.Derive(new KeyPath($"m/44'/{network.Consensus.CoinType}'/0'"));
            ExtKey addressExtKey1 = seedExtKey.Derive(new KeyPath($"m/44'/{network.Consensus.CoinType}'/1'"));
            ExtKey addressExtKey2 = seedExtKey.Derive(new KeyPath($"m/44'/{network.Consensus.CoinType}'/2'"));

            this.ExtPubKey0 = addressExtKey0.Neuter().ToString(network);
            this.ExtPubKey1 = addressExtKey1.Neuter().ToString(network);
            this.ExtPubKey2 = addressExtKey2.Neuter().ToString(network);
        }

        public PubKey GetPubKey(TokenlessWalletAccount tokenlessWalletAccount, int addressIndex, int addressType = 0)
        {
            var keyPath = new KeyPath($"{addressType}/{addressIndex}");
            ExtPubKey extPubKey = ExtPubKey.Parse(new[] { this.ExtPubKey0, this.ExtPubKey1, this.ExtPubKey2 }[(int)tokenlessWalletAccount]).Derive(keyPath);
            return extPubKey.PubKey;
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

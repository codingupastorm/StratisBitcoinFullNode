using System;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Feature.PoA.Tokenless.Wallet
{
    public interface ITokenlessWalletManager
    {
        PubKey GetPubKey(int accountIndex, int addressType = 0);

        BitcoinExtKey GetPrivateKey(string password, int accountIndex, int addressType = 0);
    }

    public class TokenlessWalletManager : ITokenlessWalletManager
    {
        public const string WalletFileName = "nodeid.json";

        private readonly Network network;
        private readonly FileStorage<TokenlessWallet> fileStorage;
        private readonly TokenlessWallet wallet;
        private readonly ExtPubKey[] extPubKeys;
        private readonly TokenlessWalletSettings walletSettings;

        public TokenlessWalletManager(Network network, DataFolder dataFolder, TokenlessWalletSettings walletSettings)
        {
            this.network = network;
            this.fileStorage = new FileStorage<TokenlessWallet>(dataFolder.RootPath);
            this.wallet = this.LoadWallet();
            this.walletSettings = walletSettings;
            if (this.wallet != null)
                this.extPubKeys = new ExtPubKey[] { ExtPubKey.Parse(this.wallet.ExtPubKey0), ExtPubKey.Parse(this.wallet.ExtPubKey1), ExtPubKey.Parse(this.wallet.ExtPubKey2) };
        }

        public TokenlessWallet LoadWallet()
        {
            string fileName = WalletFileName;

            if (!this.fileStorage.Exists(fileName))
                return null;

            return (TokenlessWallet)this.fileStorage.LoadByFileName(fileName);
        }

        public static ExtKey GetExtendedKey(Mnemonic mnemonic, string passphrase = null)
        {
            Guard.NotNull(mnemonic, nameof(mnemonic));

            return mnemonic.DeriveExtKey(passphrase);
        }

        public PubKey GetPubKey(int accountIndex, int addressType = 0)
        {
            int addressIndex = this.walletSettings.AddressIndex;
            var keyPath = new KeyPath($"{addressType}/{addressIndex}");

            ExtPubKey extPubKey = this.extPubKeys[accountIndex].Derive(keyPath);
            return extPubKey.PubKey;
        }

        public BitcoinExtKey GetPrivateKey(string password, int accountIndex, int addressType = 0)
        {
            int addressIndex = this.walletSettings.AddressIndex;
            string hdPath = $"m/44'/{this.network.Consensus.CoinType}'/{accountIndex}/{addressType}/{addressIndex}'";

            var seedExtKey = this.GetExtKey(password);

            ExtKey addressExtKey = seedExtKey.Derive(new KeyPath(hdPath));
            return addressExtKey.GetWif(this.network);
        }

        [NoTrace]
        public ExtKey GetExtKey(string password)
        {
            return new ExtKey(Key.Parse(this.wallet.EncryptedSeed, password, this.network), Convert.FromBase64String(this.wallet.ChainCode));
        }

        public (TokenlessWallet, Mnemonic) CreateWallet(string password, string passphrase, Mnemonic mnemonic = null)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotNull(passphrase, nameof(passphrase));

            // Generate the root seed used to generate keys from a mnemonic picked at random
            // and a passphrase optionally provided by the user.
            mnemonic = mnemonic ?? new Mnemonic(Wordlist.English, WordCount.Twelve);

            ExtKey extendedKey = GetExtendedKey(mnemonic, passphrase);

            // Create a wallet file.
            string encryptedSeed = extendedKey.PrivateKey.GetEncryptedBitcoinSecret(password, this.network).ToWif();
            string chainCode = Convert.ToBase64String(extendedKey.ChainCode);

            Key privateKey = Key.Parse(encryptedSeed, password, this.network);
            var seedExtKey = new ExtKey(privateKey, extendedKey.ChainCode);

            /*
            - transaction signing (m/44'/105'/0'/0/N) where N is a zero based key ID
            - block signing (m/44'/105'/1'/0/N) where N is a zero based key ID
            - P2P certificates (m/44'/105'/2'/K/N) where N is a zero based key ID
            */

            ExtKey addressExtKey0 = seedExtKey.Derive(new KeyPath($"m/44'/{this.network.Consensus.CoinType}'/0'"));
            ExtKey addressExtKey1 = seedExtKey.Derive(new KeyPath($"m/44'/{this.network.Consensus.CoinType}'/1'"));
            ExtKey addressExtKey2 = seedExtKey.Derive(new KeyPath($"m/44'/{this.network.Consensus.CoinType}'/2'"));

            ExtPubKey extPubKey0 = addressExtKey0.Neuter();
            ExtPubKey extPubKey1 = addressExtKey1.Neuter();
            ExtPubKey extPubKey2 = addressExtKey2.Neuter();

            var wallet = new TokenlessWallet()
            {
                EncryptedSeed = encryptedSeed,
                ChainCode = chainCode,
                ExtPubKey0 = extPubKey0.ToString(this.network),
                ExtPubKey1 = extPubKey1.ToString(this.network),
                ExtPubKey2 = extPubKey2.ToString(this.network)
            };

            this.fileStorage.SaveToFile(wallet, WalletFileName);

            return (wallet, mnemonic);
        }
    }
}

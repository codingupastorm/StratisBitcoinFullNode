using System;
using System.IO;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Feature.PoA.Tokenless.Wallet
{
    public interface ITokenlessWalletManager
    {
        bool Initialize();

        PubKey GetPubKey(TokenlessWalletAccount tokenlessWalletAccount, int addressType = 0);

        ExtKey GetExtKey(string password, TokenlessWalletAccount tokenlessWalletAccount, int addressType = 0);
    }

    /// <summary>
    /// - transaction signing (m/44'/105'/0'/0/N) where N is a zero based key ID
    /// - block signing(m/44'/105'/1'/0/N) where N is a zero based key ID
    /// - P2P certificates (m/44'/105'/2'/K/N) where N is a zero based key ID
    /// </summary>
    public enum TokenlessWalletAccount
    {
        TransactionSigning = 0,
        BlockSigning = 1,
        P2PCertificates = 2
    }

    public class TokenlessWalletManager : ITokenlessWalletManager
    {
        public const string WalletFileName = "nodeid.json";

        public TokenlessWallet Wallet { get; private set; }

        private readonly Network network;
        private readonly FileStorage<TokenlessWallet> fileStorage;
        private ExtPubKey[] extPubKeys;
        private readonly TokenlessWalletSettings walletSettings;

        public TokenlessWalletManager(Network network, DataFolder dataFolder, TokenlessWalletSettings walletSettings)
        {
            this.network = network;
            this.fileStorage = new FileStorage<TokenlessWallet>(dataFolder.RootPath);
            this.walletSettings = walletSettings;
        }

        public bool Initialize()
        {
            bool walletOk = this.CheckWallet();
            bool keyFileOk = this.CheckKeyFile();

            if (walletOk && keyFileOk)
                return true;

            Console.WriteLine($"Restart the daemon.");
            return false;
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

        private int GetAddressIndex(TokenlessWalletAccount tokenlessWalletAccount)
        {
            switch (tokenlessWalletAccount)
            {
                case TokenlessWalletAccount.TransactionSigning:
                    return this.walletSettings.AccountAddressIndex;

                case TokenlessWalletAccount.BlockSigning:
                    return this.walletSettings.MiningAddressIndex;

                case TokenlessWalletAccount.P2PCertificates:
                    return this.walletSettings.CertificateAddressIndex;
            }

            throw new InvalidOperationException("Undefined operation.");
        }

        public PubKey GetPubKey(TokenlessWalletAccount tokenlessWalletAccount, int addressType = 0)
        {
            int addressIndex = GetAddressIndex(tokenlessWalletAccount);
            var keyPath = new KeyPath($"{addressType}/{addressIndex}");
            ExtPubKey extPubKey = this.extPubKeys[(int)tokenlessWalletAccount].Derive(keyPath);
            return extPubKey.PubKey;
        }

        public ExtKey GetExtKey(string password, TokenlessWalletAccount tokenlessWalletAccount, int addressType = 0)
        {
            int addressIndex = GetAddressIndex(tokenlessWalletAccount);
            string hdPath = $"m/44'/{this.network.Consensus.CoinType}'/{(int)tokenlessWalletAccount}'/{addressType}/{addressIndex}";
            ExtKey seedExtKey = this.GetExtKey(password);
            ExtKey pathExtKey = seedExtKey.Derive(new KeyPath(hdPath));

            return pathExtKey;
        }

        [NoTrace]
        public ExtKey GetExtKey(string password)
        {
            return new ExtKey(Key.Parse(this.Wallet.EncryptedSeed, password, this.network), Convert.FromBase64String(this.Wallet.ChainCode));
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

        internal bool CheckWallet()
        {
            bool canStart = true;

            if (!File.Exists(Path.Combine(this.walletSettings.RootPath, WalletFileName)))
            {
                var strMnemonic = this.walletSettings.Mnemonic;
                var password = this.walletSettings.Password;

                if (password == null)
                {
                    Console.WriteLine($"Run this daemon with a -password=<password> argument so that the wallet file ({TokenlessWalletManager.WalletFileName}) can be created.");
                    Console.WriteLine($"If you are re-creating a wallet then also pass a -mnemonic=\"<mnemonic words>\" argument.");
                    return false;
                }

                TokenlessWallet wallet;
                Mnemonic mnemonic = (strMnemonic == null) ? null : new Mnemonic(strMnemonic);

                (wallet, mnemonic) = this.CreateWallet(password, password, mnemonic);

                this.Wallet = wallet;

                Console.WriteLine($"The wallet file ({TokenlessWalletManager.WalletFileName}) has been created.");
                Console.WriteLine($"Record the mnemonic ({mnemonic}) in a safe place.");
                Console.WriteLine($"IMPORTANT: You will need the mnemonic to recover the wallet.");

                // Stop the node so that the user can record the mnemonic.
                canStart = false;
            }
            else
            {
                this.Wallet = this.LoadWallet();
            }

            this.extPubKeys = new ExtPubKey[] { ExtPubKey.Parse(this.Wallet.ExtPubKey0), ExtPubKey.Parse(this.Wallet.ExtPubKey1), ExtPubKey.Parse(this.Wallet.ExtPubKey2) };

            return canStart;
        }

        internal bool CheckKeyFile()
        {
            var password = this.walletSettings.Password;

            if (!File.Exists(Path.Combine(this.walletSettings.RootPath, KeyTool.KeyFileDefaultName)))
            {
                if (password == null)
                {
                    Console.WriteLine($"Run this daemon with a -password=<password> argument so that the federation key ({KeyTool.KeyFileDefaultName}) can be created.");
                    return false;
                }

                Guard.Assert(this.Wallet != null);

                Key key = this.GetExtKey(password, TokenlessWalletAccount.BlockSigning).PrivateKey;
                var keyTool = new KeyTool(this.walletSettings.RootPath);
                keyTool.SavePrivateKey(key);

                Console.WriteLine($"The federation key ({KeyTool.KeyFileDefaultName}) has been created.");

                return false;
            }

            return true;
        }

        internal bool CheckCertificate()
        {
            var password = this.walletSettings.Password;

            if (!File.Exists(this.walletSettings.CertPath) || this.walletSettings.GenerateCertificate)
            {
                if (password == null || this.walletSettings.UserFullName == null)
                {
                    Console.WriteLine($"Run this daemon with a -password=<password> argument and certificate details configured so that the client certificate ({CertificatesManager.ClientCertificateName}) can be requested.");
                    //return false;
                }

                Guard.Assert(this.Wallet != null);

                // TODO: 4693 - Generate certificate request.
            }
            else
            {
                // TODO: 4693 - Generate certificate request (Certificate validation).
            }

            return true;
        }
    }
}

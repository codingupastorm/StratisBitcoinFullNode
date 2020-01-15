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

        /// <summary>
        /// Loads the private key for signing transactions from disk.
        /// </summary>
        /// <returns>The loaded private key.</returns>
        Key LoadTransactionSigningKey();
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
            bool blockSigningKeyFileOk = this.CheckBlockSigningKeyFile();
            bool transactionKeyFileOk = this.CheckTransactionSigningKeyFile();

            if (walletOk && blockSigningKeyFileOk && transactionKeyFileOk)
                return true;

            Console.WriteLine($"Restart the daemon.");
            return false;
        }

        public TokenlessWallet LoadWallet()
        {
            string fileName = WalletFileName;

            if (!this.fileStorage.Exists(fileName))
                return null;

            return this.fileStorage.LoadByFileName(fileName);
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
            return this.Wallet.GetPubKey(this.network, tokenlessWalletAccount, GetAddressIndex(tokenlessWalletAccount), addressType);
        }

        public ExtKey GetExtKey(string password, TokenlessWalletAccount tokenlessWalletAccount, int addressType = 0)
        {
            return this.Wallet.GetExtKey(this.network, password, tokenlessWalletAccount, addressType);
        }

        /// <inheritdoc/>
        public Key LoadTransactionSigningKey()
        {
            var transactionKeyFilePath = Path.Combine(this.walletSettings.RootPath, KeyTool.TransactionSigningKeyFileName);
            if (!File.Exists(transactionKeyFilePath))
                throw new TokenlessWalletException($"{transactionKeyFilePath} does not exist.");

            var keyTool = new KeyTool(this.walletSettings.RootPath);
            return keyTool.LoadPrivateKey(KeyType.TransactionSigningKey);
        }

        [NoTrace]
        public ExtKey GetExtKey(string password)
        {
            return new ExtKey(Key.Parse(this.Wallet.EncryptedSeed, password, this.network), Convert.FromBase64String(this.Wallet.ChainCode));
        }

        public (TokenlessWallet, Mnemonic) CreateWallet(string password, string passphrase, Mnemonic mnemonic = null)
        {
            var wallet = new TokenlessWallet(this.network, password, passphrase, ref mnemonic);

            this.fileStorage.SaveToFile(wallet, WalletFileName);

            return (wallet, mnemonic);
        }

        private bool CheckWallet()
        {
            bool canStart = true;

            if (!File.Exists(Path.Combine(this.walletSettings.RootPath, WalletFileName)))
            {
                var strMnemonic = this.walletSettings.Mnemonic;
                var password = this.walletSettings.Password;

                if (password == null)
                {
                    Console.WriteLine($"Run this daemon with a -password=<password> argument so that the wallet file ({WalletFileName}) can be created.");
                    Console.WriteLine($"If you are re-creating a wallet then also pass a -mnemonic=\"<mnemonic words>\" argument.");
                    return false;
                }

                TokenlessWallet wallet;
                Mnemonic mnemonic = (strMnemonic == null) ? null : new Mnemonic(strMnemonic);

                (wallet, mnemonic) = this.CreateWallet(password, null, mnemonic);

                this.Wallet = wallet;

                Console.WriteLine($"The wallet file ({WalletFileName}) has been created.");
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

        private bool CheckBlockSigningKeyFile()
        {
            if (!CheckPassword(KeyTool.BlockSigningKeyFileName))
                return false;

            if (!File.Exists(Path.Combine(this.walletSettings.RootPath, KeyTool.BlockSigningKeyFileName)))
            {
                Guard.Assert(this.Wallet != null);

                Key key = this.GetExtKey(this.walletSettings.Password, TokenlessWalletAccount.BlockSigning).PrivateKey;
                var keyTool = new KeyTool(this.walletSettings.RootPath);
                keyTool.SavePrivateKey(key, KeyType.BlockSigningKey);

                Console.WriteLine($"The key file '{KeyTool.BlockSigningKeyFileName}' has been created.");

                return false;
            }

            return true;
        }

        private bool CheckTransactionSigningKeyFile()
        {
            if (!CheckPassword(KeyTool.TransactionSigningKeyFileName))
                return false;

            if (!File.Exists(Path.Combine(this.walletSettings.RootPath, KeyTool.TransactionSigningKeyFileName)))
            {
                Guard.Assert(this.Wallet != null);

                Key key = this.GetExtKey(this.walletSettings.Password, TokenlessWalletAccount.TransactionSigning).PrivateKey;
                var keyTool = new KeyTool(this.walletSettings.RootPath);
                keyTool.SavePrivateKey(key, KeyType.TransactionSigningKey);

                Console.WriteLine($"The key file '{KeyTool.TransactionSigningKeyFileName}' has been created.");

                return false;
            }

            return true;
        }

        private bool CheckCertificate()
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

        private bool CheckPassword(string fileName)
        {
            if (string.IsNullOrEmpty(this.walletSettings.Password))
            {
                Console.WriteLine($"Run this daemon with a -password=<password> argument so that the '{fileName}' file can be created.");
                return false;
            }

            return true;
        }
    }
}

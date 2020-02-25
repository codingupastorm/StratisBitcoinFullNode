using System;
using System.IO;
using CertificateAuthority;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Feature.PoA.Tokenless.Wallet
{
    public interface ITokenlessWalletManager
    {
        bool Initialize();

        PubKey GetPubKey(TokenlessWalletAccount tokenlessWalletAccount, int addressType = 0);

        Key GetKey(string password, TokenlessWalletAccount tokenlessWalletAccount, int addressType = 0);

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
        private readonly DataFolder dataFolder;
        private readonly FileStorage<TokenlessWallet> fileStorage;
        private ExtPubKey[] extPubKeys;
        private readonly TokenlessWalletSettings walletSettings;
        private readonly ICertificatesManager certificatesManager;
        private readonly ILogger logger;

        public TokenlessWalletManager(Network network, DataFolder dataFolder, TokenlessWalletSettings walletSettings, ICertificatesManager certificatesManager, ILoggerFactory loggerFactory)
        {
            this.network = network;
            this.dataFolder = dataFolder;
            this.fileStorage = new FileStorage<TokenlessWallet>(this.dataFolder.RootPath);
            this.walletSettings = walletSettings;
            this.certificatesManager = certificatesManager;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public bool Initialize()
        {
            bool walletOk = this.CheckWallet();
            bool blockSigningKeyFileOk = this.CheckBlockSigningKeyFile();
            bool transactionKeyFileOk = this.CheckTransactionSigningKeyFile();
            bool certificateOk = this.CheckCertificate();

            if (walletOk && blockSigningKeyFileOk && transactionKeyFileOk && certificateOk)
                return true;

            this.logger.LogError($"Restart the daemon.");
            return false;
        }

        public TokenlessWallet LoadWallet()
        {
            string fileName = WalletFileName;

            if (!this.fileStorage.Exists(fileName))
                return null;

            return this.fileStorage.LoadByFileName(fileName);
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

        public Key GetKey(string password, TokenlessWalletAccount tokenlessWalletAccount, int addressType = 0)
        {
            return this.Wallet.GetKey(this.network, password, tokenlessWalletAccount, addressType);
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

        public (TokenlessWallet, Mnemonic) CreateWallet(string password, Mnemonic mnemonic = null)
        {
            var wallet = new TokenlessWallet(this.network, password, ref mnemonic);

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
                    this.logger.LogError($"Run this daemon with a -password=<password> argument so that the wallet file ({WalletFileName}) can be created.");
                    this.logger.LogError($"If you are re-creating a wallet then also pass a -mnemonic=\"<mnemonic words>\" argument.");
                    return false;
                }

                TokenlessWallet wallet;
                Mnemonic mnemonic = (strMnemonic == null) ? null : new Mnemonic(strMnemonic);

                (wallet, mnemonic) = this.CreateWallet(password, mnemonic);

                this.Wallet = wallet;

                this.logger.LogError($"The wallet file ({WalletFileName}) has been created.");
                this.logger.LogError($"Record the mnemonic ({mnemonic}) in a safe place.");
                this.logger.LogError($"IMPORTANT: You will need the mnemonic to recover the wallet.");

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
            if (!File.Exists(Path.Combine(this.walletSettings.RootPath, KeyTool.FederationKeyFileName)))
            {
                if (!CheckPassword(KeyTool.FederationKeyFileName))
                    return false;

                Guard.Assert(this.Wallet != null);

                Key key = this.GetKey(this.walletSettings.Password, TokenlessWalletAccount.BlockSigning);
                var keyTool = new KeyTool(this.walletSettings.RootPath);
                keyTool.SavePrivateKey(key, KeyType.FederationKey);

                this.logger.LogError($"The key file '{KeyTool.FederationKeyFileName}' has been created.");

                return false;
            }

            return true;
        }

        private bool CheckTransactionSigningKeyFile()
        {
            if (!File.Exists(Path.Combine(this.walletSettings.RootPath, KeyTool.TransactionSigningKeyFileName)))
            {
                if (!CheckPassword(KeyTool.TransactionSigningKeyFileName))
                    return false;

                Guard.Assert(this.Wallet != null);

                Key key = this.GetKey(this.walletSettings.Password, TokenlessWalletAccount.TransactionSigning);
                var keyTool = new KeyTool(this.walletSettings.RootPath);
                keyTool.SavePrivateKey(key, KeyType.TransactionSigningKey);

                this.logger.LogError($"The key file '{KeyTool.TransactionSigningKeyFileName}' has been created.");

                return false;
            }

            return true;
        }

        private bool CheckCertificate()
        {
            bool caOk = false;
            bool clientOk = false;

            try
            {
                caOk = this.certificatesManager.LoadAuthorityCertificate(false);
                clientOk = this.certificatesManager.LoadClientCertificate();
            }
            catch (CertificateConfigurationException certEx)
            {
                if (!caOk)
                {
                    Console.WriteLine(certEx.Message);

                    return false;
                }
            }

            if (clientOk && !this.walletSettings.GenerateCertificate)
                return true;

            if (!CheckPassword(CertificatesManager.ClientCertificateName))
                return false;

            // First check if we have created an account on the CA already.
            if (!this.certificatesManager.HaveAccount())
            {
                int accountId = this.certificatesManager.RequestAccount(this.walletSettings.Name,
                    this.walletSettings.OrganizationUnit,
                    this.walletSettings.Organization,
                    this.walletSettings.Locality,
                    this.walletSettings.StateOrProvince,
                    this.walletSettings.EmailAddress,
                    this.walletSettings.Country);

                // The CA admin will need to approve the account, so advise the user.
                this.logger.LogError($"Account created with ID {accountId}. After account approval, please update the node configuration and restart to proceed.");

                return false;
            }

            // The certificate manager is responsible for creation and storage of the client certificate, the wallet manager is primarily responsible for providing the requisite private key.
            Key privateKey = this.GetKey(this.walletSettings.Password, TokenlessWalletAccount.P2PCertificates);
            PubKey transactionSigningPubKey = this.GetKey(this.walletSettings.Password, TokenlessWalletAccount.TransactionSigning).PubKey;
            PubKey blockSigningPubKey = this.GetKey(this.walletSettings.Password, TokenlessWalletAccount.BlockSigning).PubKey;

            X509Certificate clientCert = this.certificatesManager.RequestNewCertificate(privateKey, transactionSigningPubKey, blockSigningPubKey);

            File.WriteAllBytes(Path.Combine(this.dataFolder.RootPath, CertificatesManager.ClientCertificateName), CaCertificatesManager.CreatePfx(clientCert, privateKey, this.walletSettings.Password));

            return true;
        }

        private bool CheckPassword(string fileName)
        {
            if (string.IsNullOrEmpty(this.walletSettings.Password))
            {
                this.logger.LogError($"Run this daemon with a -password=<password> argument so that the '{fileName}' file can be created.");

                return false;
            }

            return true;
        }
    }
}

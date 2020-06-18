using System;
using System.IO;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Configuration;
using Stratis.Core.Utilities;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Features.PoA;

namespace Stratis.Feature.PoA.Tokenless.KeyStore
{
    public interface ITokenlessKeyStoreManager
    {
        bool Initialize();

        PubKey GetPubKey(TokenlessKeyStoreAccount account, int addressType = 0);

        Key GetKey(string password, TokenlessKeyStoreAccount account, int addressType = 0);

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
    public enum TokenlessKeyStoreAccount
    {
        TransactionSigning = 0,
        BlockSigning = 1,
        P2PCertificates = 2
    }

    public class TokenlessKeyStoreManager : ITokenlessKeyStoreManager
    {
        public const string KeyStoreFileName = "nodeid.json";

        public TokenlessKeyStore KeyStore { get; private set; }

        private readonly Network network;
        private readonly DataFolder dataFolder;
        private readonly FileStorage<TokenlessKeyStore> fileStorage;
        private readonly ChannelSettings channelSettings;
        private readonly TokenlessKeyStoreSettings keyStoreSettings;
        private readonly ILogger logger;

        public TokenlessKeyStoreManager(Network network, DataFolder dataFolder, ChannelSettings channelSettings, TokenlessKeyStoreSettings keyStoreSettings, ILoggerFactory loggerFactory)
        {
            this.channelSettings = channelSettings;
            this.dataFolder = dataFolder;
            this.keyStoreSettings = keyStoreSettings;
            this.network = network;

            this.fileStorage = new FileStorage<TokenlessKeyStore>(this.dataFolder.RootPath);
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public bool Initialize()
        {
            this.logger.LogInformation($"Initializing the keystore; channel node is {this.channelSettings.IsChannelNode}");
            this.logger.LogInformation($"Initializing the keystore; system channel node is {this.channelSettings.IsSystemChannelNode}");
            this.logger.LogInformation($"Initializing the keystore; infra node is {this.channelSettings.IsInfraNode}");

            bool keyStoreOk = this.CheckKeyStore();
            bool blockSigningKeyFileOk = this.CheckBlockSigningKeyFile();
            bool transactionKeyFileOk = this.CheckTransactionSigningKeyFile();

            if (keyStoreOk && blockSigningKeyFileOk && transactionKeyFileOk)
                return true;

            this.logger.LogWarning($"Restart the daemon.");

            return false;
        }

        public TokenlessKeyStore LoadKeyStore()
        {
            string fileName = KeyStoreFileName;

            if (!this.fileStorage.Exists(fileName))
                return null;

            return this.fileStorage.LoadByFileName(fileName);
        }

        private int GetAddressIndex(TokenlessKeyStoreAccount account)
        {
            switch (account)
            {
                case TokenlessKeyStoreAccount.TransactionSigning:
                    return this.keyStoreSettings.AccountAddressIndex;

                case TokenlessKeyStoreAccount.BlockSigning:
                    return this.keyStoreSettings.MiningAddressIndex;

                case TokenlessKeyStoreAccount.P2PCertificates:
                    return this.keyStoreSettings.CertificateAddressIndex;
            }

            throw new InvalidOperationException("Undefined operation.");
        }

        public PubKey GetPubKey(TokenlessKeyStoreAccount account, int addressType = 0)
        {
            return this.KeyStore.GetPubKey(this.network, account, GetAddressIndex(account), addressType);
        }

        public Key GetKey(string password, TokenlessKeyStoreAccount account, int addressType = 0)
        {
            return this.KeyStore.GetKey(this.network, password, account, addressType);
        }

        /// <inheritdoc/>
        public Key LoadTransactionSigningKey()
        {
            var transactionKeyFilePath = Path.Combine(this.keyStoreSettings.RootPath, KeyTool.TransactionSigningKeyFileName);
            if (!File.Exists(transactionKeyFilePath))
                throw new TokenlessKeyStoreException($"{transactionKeyFilePath} does not exist.");

            var keyTool = new KeyTool(this.keyStoreSettings.RootPath);
            return keyTool.LoadPrivateKey(KeyType.TransactionSigningKey);
        }

        public (TokenlessKeyStore, Mnemonic) CreateKeyStore(string password, Mnemonic mnemonic = null)
        {
            var keyStore = new TokenlessKeyStore(this.network, password, ref mnemonic);

            this.fileStorage.SaveToFile(keyStore, KeyStoreFileName);

            return (keyStore, mnemonic);
        }

        private bool CheckKeyStore()
        {
            bool canStart = true;

            var keyStorePath = Path.Combine(this.keyStoreSettings.RootPath, KeyStoreFileName);

            this.logger.LogInformation($"Checking if the keystore exists at: {keyStorePath}");

            if (!File.Exists(keyStorePath))
            {
                this.logger.LogInformation($"Key store does not exist, creating...");

                if (this.keyStoreSettings.KeyStorePassword == null)
                {
                    this.logger.LogError($"Run this daemon with a -keystorepassword=<password> argument so that the keystore file ({KeyStoreFileName}) can be created.");
                    this.logger.LogError($"If you are re-creating a keystore then also pass a -mnemonic=\"<mnemonic words>\" argument.");
                    return false;
                }

                TokenlessKeyStore keyStore;
                Mnemonic mnemonic = (this.keyStoreSettings.Mnemonic == null) ? null : new Mnemonic(this.keyStoreSettings.Mnemonic);

                (keyStore, mnemonic) = this.CreateKeyStore(this.keyStoreSettings.KeyStorePassword, mnemonic);

                this.KeyStore = keyStore;

                this.logger.LogInformation($"The key store file '{KeyStoreFileName}' has been created.");
                this.logger.LogInformation($"Record the mnemonic '{mnemonic}' in a safe place.");
                this.logger.LogInformation($"IMPORTANT: You will need the mnemonic to recover the key store.");

                // Only stop the node if this node is not a channel node.
                if (!this.channelSettings.IsChannelNode)
                    canStart = false; // Stop the node so that the user can record the mnemonic.
            }
            else
            {
                this.KeyStore = this.LoadKeyStore();
            }

            return canStart;
        }

        private bool CheckBlockSigningKeyFile()
        {
            var path = Path.Combine(this.keyStoreSettings.RootPath, KeyTool.FederationKeyFileName);

            this.logger.LogInformation($"Checking if the block signing key file exists at: {path}");

            if (!File.Exists(path))
            {
                this.logger.LogInformation($"Block signing key file does not exist, creating...");

                if (!CheckPassword(KeyTool.FederationKeyFileName))
                    return false;

                Guard.Assert(this.KeyStore != null);

                Key key = this.GetKey(this.keyStoreSettings.KeyStorePassword, TokenlessKeyStoreAccount.BlockSigning);
                var keyTool = new KeyTool(this.keyStoreSettings.RootPath);
                keyTool.SavePrivateKey(key, KeyType.FederationKey);

                this.logger.LogInformation($"The key file '{KeyTool.FederationKeyFileName}' has been created.");

                // Only stop the node if this node is not a channel node.
                if (!this.channelSettings.IsChannelNode)
                    return false;
            }

            return true;
        }

        private bool CheckTransactionSigningKeyFile()
        {
            var path = Path.Combine(this.keyStoreSettings.RootPath, KeyTool.TransactionSigningKeyFileName);

            this.logger.LogInformation($"Checking if the transaction signing key file exists at: {path}");

            if (!File.Exists(path))
            {
                this.logger.LogInformation($"Transaction signing key file does not exist, creating...");

                if (!CheckPassword(KeyTool.TransactionSigningKeyFileName))
                    return false;

                Guard.Assert(this.KeyStore != null);

                Key key = this.GetKey(this.keyStoreSettings.KeyStorePassword, TokenlessKeyStoreAccount.TransactionSigning);
                var keyTool = new KeyTool(this.keyStoreSettings.RootPath);
                keyTool.SavePrivateKey(key, KeyType.TransactionSigningKey);

                this.logger.LogInformation($"The key file '{KeyTool.TransactionSigningKeyFileName}' has been created.");

                // Only stop the node if this node is not a channel node.
                if (!this.channelSettings.IsChannelNode)
                    return false;
            }

            return true;
        }

        private bool CheckPassword(string fileName)
        {
            if (string.IsNullOrEmpty(this.keyStoreSettings.KeyStorePassword))
            {
                this.logger.LogError($"Run this daemon with a -password=<password> argument so that the '{fileName}' file can be created.");
                return false;
            }

            return true;
        }
    }
}

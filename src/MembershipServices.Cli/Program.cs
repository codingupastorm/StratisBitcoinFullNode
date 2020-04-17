using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using CertificateAuthority;
using CommandLine;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Configuration;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.Features.PoA.ProtocolEncryption;

namespace MembershipServices.Cli
{
    public class Program
    {
        [Verb("help", HelpText = "Show help.")]
        class HelpOptions
        {
        }

        [Verb("generate", HelpText = "Generate node certificate and any requisite key material.")]
        class GenerateOptions
        {
            [Option("datadir", Required = true, HelpText = "The location of the underlying node's root folder.")]
            public string DataDir { get; set; }

            [Option("commonname", Required = true, HelpText = "The unique identifier for this node within its organization.")]
            public string CommonName { get; set; }

            [Option("organization", Required = true, HelpText = "The organization that this node belongs to.")]
            public string Organization { get; set; }

            [Option("organizationunit", Required = true, HelpText = "The organization unit that this node belongs to.")]
            public string OrganizationUnit { get; set; }

            [Option("locality", Required = true, HelpText = "The locality of the entity this node belongs to.")]
            public string Locality { get; set; }

            [Option("stateorprovince", Required = true, HelpText = "The state or province of the entity this node belongs to.")]
            public string StateOrProvince { get; set; }

            [Option("emailaddress", Required = true, HelpText = "The email address of the node operator.")]
            public string EmailAddress { get; set; }

            [Option("country", Required = true, HelpText = "The country of the entity this node belongs to.")]
            public string Country { get; set; }

            [Option("caurl", Required = false, Default = "https://localhost:5001", HelpText = "The URL of the certificate authority.")]
            public string CaUrl { get; set; }

            [Option("caaccountid", Required = false, HelpText = "The account ID of the user requesting a certificate from the certificate authority.")]
            public string CaAccountId { get; set; }

            [Option("capassword", Required = true, HelpText = "The account password of the user requesting a certificate from the certificate authority.")]
            public string CaPassword { get; set; }

            [Option("password", Required = true, HelpText = "The password for the node's keystore.")]
            public string Password { get; set; }

            /// <summary>
            /// See <see cref="CaCertificatesManager.ValidPermissions"/> for the valid permission names.
            /// </summary>
            [Option("requestedpermissions", Required = true, HelpText = "The set of permissions being requested by this node.")]
            public IEnumerable<string> RequestedPermissions { get; set; }
        }

        [Verb("showtemplate", HelpText = "Show the default configuration template.")]
        class ShowTemplateOptions
        {
        }

        [Verb("version", HelpText = "Show version information.")]
        class VersionOptions
        {
        }

        [Verb("extend", HelpText = "Extend existing network.")]
        class ExtendOptions
        {
            [Option("certificatepath", Required = true, HelpText = "The path to the certificate file that needs to be added to the network.")]
            public string CertificatePath { get; set; }

            [Option("type", Required = true, HelpText = "The type of certificate being added, e.g. NetworkPeer.")]
            public string Type { get; set; }
        }

        static int RunHelp(HelpOptions options)
        {
            return 0;
        }

        static int RunGenerate(GenerateOptions options)
        {
            // TODO: Move this logic into a reusable method
            var network = new TokenlessNetwork();
            var nodeSettings = new NodeSettings(network, args: new [] { $"-datadir={options.DataDir}", $"-password={options.Password}", $"-caaccountid={options.CaAccountId}", $"-capassword={options.CaPassword}" });
            var loggerFactory = new LoggerFactory();

            var membershipServices = new MembershipServicesDirectory(nodeSettings);
            membershipServices.Initialize();

            var revocationChecker = new RevocationChecker(membershipServices);
            var certificatesManager = new CertificatesManager(nodeSettings.DataFolder, nodeSettings, loggerFactory, revocationChecker, network);
            var keyStoreSettings = new TokenlessKeyStoreSettings(nodeSettings);
            var keyStoreManager = new TokenlessKeyStoreManager(network, nodeSettings.DataFolder, keyStoreSettings, certificatesManager, loggerFactory);
            keyStoreManager.Initialize();

            // First check if we have created an account on the CA already.
            if (string.IsNullOrWhiteSpace(options.CaAccountId))
            {
                var caClient = new CaClient(new Uri(options.CaUrl), new HttpClient(), 0, options.CaPassword);

                int accountId = caClient.CreateAccount(options.CommonName,
                    options.OrganizationUnit,
                    options.Organization,
                    options.Locality,
                    options.StateOrProvince,
                    options.EmailAddress,
                    options.Country,
                    options.RequestedPermissions.ToArray());

                // The CA admin will need to approve the account, so advise the user.
                Console.WriteLine($"Account created with ID {accountId}. After account approval, please update the configuration and restart to proceed.");

                return -1;
            }

            Key privateKey = keyStoreManager.GetKey(keyStoreSettings.Password, TokenlessKeyStoreAccount.P2PCertificates);

            File.WriteAllText(Path.Combine(nodeSettings.DataFolder.RootPath, LocalMembershipServicesConfiguration.Keystore, "key.dat"), privateKey.GetBitcoinSecret(network).ToWif());

            PubKey transactionSigningPubKey = keyStoreManager.GetKey(keyStoreSettings.Password, TokenlessKeyStoreAccount.TransactionSigning).PubKey;
            PubKey blockSigningPubKey = keyStoreManager.GetKey(keyStoreSettings.Password, TokenlessKeyStoreAccount.BlockSigning).PubKey;

            X509Certificate clientCert = certificatesManager.RequestNewCertificate(privateKey, transactionSigningPubKey, blockSigningPubKey);

            if (clientCert != null)
            {
                membershipServices.AddLocalMember(clientCert, MemberType.Self);

                // We need the certificate to be available here as well for now.
                membershipServices.AddLocalMember(clientCert, MemberType.NetworkPeer);

                // TODO: Temporary workaround until CertificatesManager is completely removed and merged into MSD
                File.WriteAllBytes(Path.Combine(nodeSettings.DataFolder.RootPath, CertificatesManager.ClientCertificateName), CaCertificatesManager.CreatePfx(clientCert, privateKey, keyStoreSettings.Password));

                return 0;
            }

            return -1;
        }

        static int RunShowTemplate(ShowTemplateOptions options)
        {
            throw new NotImplementedException();
        }

        static int RunVersion(VersionOptions options)
        {
            throw new NotImplementedException();
        }

        static int RunExtend(ExtendOptions options)
        {
            var network = new TokenlessNetwork();
            var nodeSettings = new NodeSettings(network);

            var membershipServices = new MembershipServicesDirectory(nodeSettings);
            membershipServices.Initialize();

            MemberType memberType;
            switch (options.Type)
            {
                case "Admin":
                    memberType = MemberType.Admin;
                    break;
                case "IntermediateCA":
                    memberType = MemberType.IntermediateCA;
                    break;
                case "RootCA":
                    memberType = MemberType.RootCA;
                    break;
                case "NetworkPeer":
                    memberType = MemberType.NetworkPeer;
                    break;
                case "Self":
                    // This should normally not be needed, as the generate verb makes the node's own certificate and adds it to its MSD
                    memberType = MemberType.Self;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var parser = new X509CertificateParser();

            try
            {
                X509Certificate certificate = parser.ReadCertificate(File.ReadAllBytes(options.CertificatePath));

                membershipServices.AddLocalMember(certificate, memberType);
            }
            catch
            {
                return -1;
            }

            return 0;
        }

        public static void Main(string[] args)
        {
            // https://hyperledger-fabric.readthedocs.io/en/release-2.0/commands/cryptogen.html
            Parser.Default.ParseArguments<HelpOptions, GenerateOptions, ShowTemplateOptions, VersionOptions, ExtendOptions>(args)
                .MapResult(
                    (HelpOptions opts) => RunHelp(opts),
                    (GenerateOptions opts) => RunGenerate(opts),
                    (ShowTemplateOptions opts) => RunShowTemplate(opts),
                    (VersionOptions opts) => RunVersion(opts),
                    (ExtendOptions opts) => RunExtend(opts),
                    errs => 1);
        }
    }
}
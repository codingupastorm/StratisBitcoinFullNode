using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using CertificateAuthority;
using CommandLine;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Feature.PoA.Tokenless;
using Stratis.Feature.PoA.Tokenless.Wallet;
using MembershipServices;
using NBitcoin;
using Org.BouncyCastle.X509;

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

            [Option("password", Required = true, HelpText = "The account password of the user requesting a certificate from the certificate authority.")]
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
        }

        static int RunHelp(HelpOptions options)
        {
            return 0;
        }

        static int RunGenerate(GenerateOptions options)
        {
            // TODO: Move this logic into a reusable method
            var network = new TokenlessNetwork();
            var nodeSettings = new NodeSettings(network);
            var loggerFactory = new LoggerFactory();

            var membershipServices = new MembershipServicesDirectory(nodeSettings);
            membershipServices.Initialize();

            var revocationChecker = new RevocationChecker(membershipServices);
            var certificatesManager = new CertificatesManager(nodeSettings.DataFolder, nodeSettings, loggerFactory, revocationChecker, network);
            var walletSettings = new TokenlessWalletSettings(nodeSettings);
            var walletManager = new TokenlessWalletManager(network, nodeSettings.DataFolder, walletSettings, certificatesManager, loggerFactory);

            // First check if we have created an account on the CA already.
            if (string.IsNullOrWhiteSpace(options.CaAccountId))
            {
                var caClient = new CaClient(new Uri(options.CaUrl), new HttpClient(), 0, options.Password);

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

            // The certificate manager is responsible for creation and storage of the client certificate, the wallet manager is primarily responsible for providing the requisite private key.
            Key privateKey = walletManager.GetKey(walletSettings.Password, TokenlessWalletAccount.P2PCertificates);
            PubKey transactionSigningPubKey = walletManager.GetKey(walletSettings.Password, TokenlessWalletAccount.TransactionSigning).PubKey;
            PubKey blockSigningPubKey = walletManager.GetKey(walletSettings.Password, TokenlessWalletAccount.BlockSigning).PubKey;

            X509Certificate clientCert = certificatesManager.RequestNewCertificate(privateKey, transactionSigningPubKey, blockSigningPubKey);

            File.WriteAllBytes(Path.Combine(nodeSettings.DataFolder.RootPath, CertificatesManager.ClientCertificateName), CaCertificatesManager.CreatePfx(clientCert, privateKey, walletSettings.Password));
            
            return 0;
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
            throw new NotImplementedException();
        }

        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<HelpOptions, GenerateOptions, ShowTemplateOptions, VersionOptions, ExtendOptions>(args)
                .MapResult(
                    (HelpOptions opts) => RunHelp(opts),
                    (GenerateOptions opts) => RunGenerate(opts),
                    (ShowTemplateOptions opts) => RunShowTemplate(opts),
                    (VersionOptions opts) => RunVersion(opts),
                    (ExtendOptions opts) => RunExtend(opts),
                    errs => 1);

            try
            {
                // The cryptogen command has five subcommands, as follows:
                // help
                // generate
                // showtemplate
                // extend
                // version

                /*
                usage: cryptogen [<flags>] <command> [<args> ...]

                Utility for generating Hyperledger Fabric key material

                Flags:
                  --help  Show context-sensitive help (also try --help-long and --help-man).

                Commands:
                  help [<command>...]
                    Show help.

                  generate [<flags>]
                    Generate key material

                  showtemplate
                    Show the default configuration template

                  version
                    Show version information

                  extend [<flags>]
                    Extend existing network
                 */

                // https://hyperledger-fabric.readthedocs.io/en/release-2.0/commands/cryptogen.html
            }
            catch (Exception ex)
            {
                Console.WriteLine("There was a problem initializing the node : '{0}'", ex.ToString());
            }
        }
    }
}

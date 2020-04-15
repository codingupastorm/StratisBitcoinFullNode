using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MembershipServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.Features.PoA.ProtocolEncryption;
using Stratis.SmartContracts.Core.Util;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class EndorsementInfoTests
    {
        private TokenlessSigner tokenlessSigner;
        private CertificatePermissionsChecker certificatePermissionsChecker;
        private MembershipServicesDirectory membershipServices;

        public EndorsementSigner GetSigner(Network network = null, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            network = network ?? new TokenlessNetwork();

            string testDir = TestBase.GetTestDirectoryPath(this, callingMethod);
            var settings = new NodeSettings(network, args: new[] { $"datadir={testDir}", "password=test" });
            this.membershipServices = new MembershipServicesDirectory(settings);
            var revocationChecker = new RevocationChecker(membershipServices);
            var certificatesManager = new CertificatesManager(settings.DataFolder, settings, settings.LoggerFactory, revocationChecker, network);
            var tokenlessWalletManager = new TokenlessKeyStoreManager(network, settings.DataFolder, new TokenlessKeyStoreSettings(settings), certificatesManager, settings.LoggerFactory);
            tokenlessWalletManager.Initialize();
            this.tokenlessSigner = new TokenlessSigner(network, new SenderRetriever());
            this.certificatePermissionsChecker =
                new CertificatePermissionsChecker(membershipServices, certificatesManager, new LoggerFactory());
            return new EndorsementSigner(network, this.tokenlessSigner, tokenlessWalletManager);
        }

        [Fact]
        public void New_Endorsement_Has_State_Proposed()
        {
            Assert.Equal(EndorsementState.Proposed, new EndorsementInfo().State);
        }

        [Fact]
        public void Endorsement_Gets_Address_Org_From_Transaction()
        {
            var endorsement = new EndorsementInfo();
            var network = new TokenlessNetwork();
            var tx = network.CreateTransaction();
            Script outputScript = TxReadWriteDataTemplate.Instance.GenerateScriptPubKey(new byte[] { 0xAA });
            tx.Outputs.Add(new TxOut(Money.Zero, outputScript));
            var signer = GetSigner();
            signer.Sign(tx);

            var addr = this.tokenlessSigner.GetSender(tx);
            var cert = this.membershipServices.GetCertificateForAddress(addr.Sender);
        }

        [Fact]
        public void MofNPolicyValidator_Validates_No_Signature_Correctly()
        {
            var policy = new Dictionary<Organisation, int>();
            var validator = new MofNPolicyValidator(policy);

            Assert.True(validator.Valid);
        }

        [Fact]
        public void MofNPolicyValidator_Validates_One_Signature_One_Organisation_Correctly()
        {
            var policy = new Dictionary<Organisation, int>();
            var validator = new MofNPolicyValidator(policy);

            Assert.True(validator.Valid);
        }

        [Fact]
        public void MofNPolicyValidator_Validates_Multiple_Signatures_One_Organisation_Correctly()
        {
            var org = (Organisation) "Test";
            var policy = new Dictionary<Organisation, int>
            {
                { org, 2 }
            };

            var validator = new MofNPolicyValidator(policy);

            validator.AddSignature(org, "test");

            Assert.False(validator.Valid);

            validator.AddSignature(org, "test2");

            Assert.True(validator.Valid);
        }

        [Fact]
        public void MofNPolicyValidator_Validates_Multiple_Signatures_Multiple_Organisations_Correctly()
        {
            var org = (Organisation)"Test";
            var org2 = (Organisation)"Test2";
            var policy = new Dictionary<Organisation, int>
            {
                { org, 2 },
                { org2, 3 }
            };

            var validator = new MofNPolicyValidator(policy);

            // Add org 1 signatures
            validator.AddSignature(org, "test");
            validator.AddSignature(org, "test 2");

            Assert.False(validator.Valid);

            // Add org 2 signatures

            validator.AddSignature(org2, "test2 2");
            validator.AddSignature(org2, "test2 3");
            validator.AddSignature(org2, "test2 4");

            Assert.True(validator.Valid);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Castle.Components.DictionaryAdapter;
using Moq;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.SmartContracts.Core.ReadWrite;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class EndorsementInfoTests
    {
        [Fact]
        public void New_Endorsement_Has_State_Proposed()
        {
            Assert.Equal(EndorsementState.Proposed, new EndorsementInfo(new Dictionary<Organisation, int>(), Mock.Of<IOrganisationLookup>(), Mock.Of<ICertificatePermissionsChecker>(), new TokenlessNetwork()).State);
        }

        [Fact]
        public void Endorsement_Gets_Address_Org_From_Transaction()
        {
            var organisationLookup = new Mock<IOrganisationLookup>();
            var permissionsChecker = Mock.Of<ICertificatePermissionsChecker>();
            var network = new TokenlessNetwork();

            var organisation = (Organisation) "ORG_1_DN";
            var senderAddress = "SENDER";
            var transaction = new Transaction();

            // Rig the lookup to return what we want.
            organisationLookup
                .Setup(l => l.FromTransaction(It.IsAny<Transaction>()))
                .Returns((organisation, senderAddress));

            // Basic policy that only requires 1 sig from organisation.
            var basicPolicy = new Dictionary<Organisation, int>
            {
                { organisation, 1 }
            };

            var endorsement = new EndorsementInfo(basicPolicy, organisationLookup.Object, permissionsChecker, network);

            Assert.False(endorsement.Validate());
            Assert.Equal(EndorsementState.Proposed, endorsement.State);
            
            endorsement.AddSignature(transaction);

            Assert.True(endorsement.Validate());
            Assert.Equal(EndorsementState.Approved, endorsement.State);
            organisationLookup.Verify(l => l.FromTransaction(transaction), Times.Once);
        }

        [Fact]
        public void Endorsement_Gets_Address_Unapproved_Org_From_Transaction()
        {
            var organisationLookup = new Mock<IOrganisationLookup>();
            var permissionsChecker = Mock.Of<ICertificatePermissionsChecker>();
            var network = new TokenlessNetwork();

            var organisation = (Organisation)"ORG_1_DN";
            var unapprovedOrg = (Organisation) "BAD_ORG_DN";
            var senderAddress = "SENDER";
            var transaction = new Transaction();

            // Rig the lookup to return what we want.
            organisationLookup
                .Setup(l => l.FromTransaction(It.IsAny<Transaction>()))
                .Returns((unapprovedOrg, senderAddress));

            // Basic policy that only requires 1 sig from the valid organisation.
            var basicPolicy = new Dictionary<Organisation, int>
            {
                { organisation, 1 }
            };

            var endorsement = new EndorsementInfo(basicPolicy, organisationLookup.Object, permissionsChecker, network);

            Assert.False(endorsement.Validate());
            Assert.Equal(EndorsementState.Proposed, endorsement.State);

            endorsement.AddSignature(transaction);

            Assert.False(endorsement.Validate());
            Assert.Equal(EndorsementState.Proposed, endorsement.State);
            organisationLookup.Verify(l => l.FromTransaction(transaction), Times.Once);
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
            var org = (Organisation)"Test";
            var org2 = (Organisation)"Test2";
            var policy = new Dictionary<Organisation, int>
            {
                { org, 1 }
            };

            var validator = new MofNPolicyValidator(policy);

            validator.AddSignature(org2, "test");

            Assert.False(validator.Valid);

            validator.AddSignature(org, "test");

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


        [Fact]
        public void Signed_Proposal_Response_Roundtrip()
        {
            var proposalResponse = new SignedProposalResponse
            {
                ProposalResponse = new ProposalResponse
                {
                    ReadWriteSet = new ReadWriteSet
                    {
                        Reads = new List<ReadItem>
                        {
                            new ReadItem { ContractAddress = uint160.One, Key = new byte[] { 0xCC, 0xCC, 0xCC }, Version = "1"}
                        },
                        Writes = new List<WriteItem>
                        {
                            new WriteItem { ContractAddress = uint160.One, IsPrivateData = false, Key = new byte[] { 0xDD, 0xDD, 0xDD }, Value = new byte[] { 0xEE, 0xEE, 0xEE }}
                        }
                    }
                },
                Endorsement = new Endorsement.Endorsement(new byte[] {0xAA, 0xAA, 0xAA}, new byte[] {0xBB, 0xBB, 0XBB})
            };

            var toBytes = proposalResponse.ToBytes();

            var fromBytes = SignedProposalResponse.FromBytes(toBytes);

            // Full roundtrip serialize and compare.
            Assert.True(toBytes.SequenceEqual(fromBytes.ToBytes()));
        }
    }
}

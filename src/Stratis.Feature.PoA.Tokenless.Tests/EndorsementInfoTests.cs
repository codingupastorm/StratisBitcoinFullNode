using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.SmartContracts.Core.Endorsement;
using Stratis.SmartContracts.Core.ReadWrite;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class EndorsementInfoTests
    {
        [Fact]
        public void New_Endorsement_Has_State_Proposed()
        {
            Assert.Equal(EndorsementState.Proposed, new EndorsementInfo(new EndorsementPolicy(), Mock.Of<IOrganisationLookup>(), Mock.Of<IEndorsementSignatureValidator>()).State);
        }

        [Fact]
        public void Endorsement_Gets_Address_Org_From_Certificate()
        {
            var organisationLookup = new Mock<IOrganisationLookup>();
            var endorsementValidator = new Mock<IEndorsementSignatureValidator>();

            var certParser = new X509CertificateParser();
            X509Certificate certificate = certParser.ReadCertificate(File.ReadAllBytes("Certificates/cert.crt"));

            var organisation = (Organisation)certificate.GetOrganisation();
            var senderAddress = "SENDER";

            // Rig the lookup to return what we want.
            organisationLookup
                .Setup(l => l.FromCertificate(It.IsAny<X509Certificate>()))
                .Returns((organisation, senderAddress));

            endorsementValidator.Setup(e => e.Validate(It.IsAny<Endorsement.Endorsement>(), It.IsAny<byte[]>()))
                .Returns(true);

            // Basic policy that only requires 1 sig from organisation.
            var basicPolicy = new EndorsementPolicy
            {
                Organisation = organisation,
                RequiredSignatures = 1
            };

            var key = new Key();
            var signature = key.Sign(new uint256()); // Some random sig

            var endorsement = new EndorsementInfo(basicPolicy, organisationLookup.Object, endorsementValidator.Object);
            var proposalResponse = new SignedProposalResponse
            {
                ProposalResponse = new ProposalResponse
                {
                    ReadWriteSet = new ReadWriteSet()
                },
                Endorsement = new Endorsement.Endorsement(signature.ToDER(), key.PubKey.ToBytes())
            };

            Assert.False(endorsement.Validate());
            Assert.Equal(EndorsementState.Proposed, endorsement.State);

            Assert.True(endorsement.AddSignature(certificate, proposalResponse));

            Assert.True(endorsement.Validate());
            Assert.Equal(EndorsementState.Approved, endorsement.State);

            endorsementValidator.Verify(p => p.Validate(proposalResponse.Endorsement, It.Is<byte[]>(v => proposalResponse.ProposalResponse.ToBytes().SequenceEqual(v))));

            organisationLookup.Verify(l => l.FromCertificate(certificate), Times.Once);
        }

        [Fact]
        public void Endorsement_Gets_Address_Unapproved_Org_From_Transaction()
        {
            var organisationLookup = new Mock<IOrganisationLookup>();
            var endorsementValidator = new Mock<IEndorsementSignatureValidator>();

            var certParser = new X509CertificateParser();
            X509Certificate certificate = certParser.ReadCertificate(File.ReadAllBytes("Certificates/cert.crt"));

            var unapprovedOrganisation = (Organisation)certificate.GetOrganisation();
            var approvedOrganisation = (Organisation)"Approved";
            var senderAddress = "SENDER";

            // Rig the lookup to return what we want.
            organisationLookup
                .Setup(l => l.FromCertificate(It.IsAny<X509Certificate>()))
                .Returns((unapprovedOrganisation, senderAddress));

            endorsementValidator.Setup(e => e.Validate(It.IsAny<Endorsement.Endorsement>(), It.IsAny<byte[]>()))
                .Returns(true);

            // Basic policy that only requires 1 sig from organisation.
            var basicPolicy = new EndorsementPolicy
            {
                Organisation = approvedOrganisation,
                RequiredSignatures = 1
            };

            var key = new Key();
            var signature = key.Sign(new uint256()); // Some random sig

            var endorsement = new EndorsementInfo(basicPolicy, organisationLookup.Object, endorsementValidator.Object);
            var proposalResponse = new SignedProposalResponse
            {
                ProposalResponse = new ProposalResponse
                {
                    ReadWriteSet = new ReadWriteSet()
                },
                Endorsement = new Endorsement.Endorsement(signature.ToDER(), key.PubKey.ToBytes())
            };

            Assert.False(endorsement.Validate());
            Assert.Equal(EndorsementState.Proposed, endorsement.State);

            // Adding sig should still work as it's valid for the cert.
            Assert.True(endorsement.AddSignature(certificate, proposalResponse));

            // Validation fails because the org is not valid.
            Assert.False(endorsement.Validate());
            Assert.Equal(EndorsementState.Proposed, endorsement.State);

            endorsementValidator.Verify(p => p.Validate(proposalResponse.Endorsement, proposalResponse.ProposalResponse.ToBytes()), Times.Never);
            organisationLookup.Verify(l => l.FromCertificate(certificate), Times.Once);
        }

        [Fact]
        public void MofNPolicyValidator_Validates_No_Signature_Correctly()
        {
            var policy = new EndorsementPolicy();
            var validator = new MofNPolicyValidator(policy.ToDictionary());

            Assert.True(validator.Valid);
        }

        [Fact]
        public void MofNPolicyValidator_Validates_One_Signature_One_Organisation_Correctly()
        {
            var org = (Organisation)"Test";
            var org2 = (Organisation)"Test2";
            var policy = new EndorsementPolicy
            {
                Organisation = org,
                RequiredSignatures = 1
            };

            var validator = new MofNPolicyValidator(policy.ToDictionary());

            validator.AddSignature(org2, "test");

            Assert.False(validator.Valid);

            validator.AddSignature(org, "test");

            Assert.True(validator.Valid);
            Assert.Single(validator.GetValidAddresses());
        }

        [Fact]
        public void MofNPolicyValidator_Validates_Multiple_Signatures_One_Organisation_Correctly()
        {
            var org = (Organisation)"Test";
            var policy = new EndorsementPolicy
            {
                Organisation = org,
                RequiredSignatures = 2
            };

            var validator = new MofNPolicyValidator(policy.ToDictionary());

            validator.AddSignature(org, "test");

            Assert.False(validator.Valid);

            validator.AddSignature(org, "test2");

            Assert.True(validator.Valid);
            Assert.Equal("test", validator.GetValidAddresses()[0]);
            Assert.Equal("test2", validator.GetValidAddresses()[1]);
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
        public void MofNPolicyValidator_Returns_Valid_Addresses_Correctly()
        {
            var org = (Organisation)"Test";
            var org2 = (Organisation)"Test2";
            var policy = new Dictionary<Organisation, int>
            {
                { org, 2 },
            };

            var validator = new MofNPolicyValidator(policy);

            // Add org 2 signatures - they don't contribute to the policy being valid
            validator.AddSignature(org2, "test2 2");
            validator.AddSignature(org2, "test2 3");
            validator.AddSignature(org2, "test2 4");
            Assert.False(validator.Valid);

            // Add org 1 signatures
            validator.AddSignature(org, "test");
            validator.AddSignature(org, "test 2");

            Assert.True(validator.Valid);

            // Ensure only org 1 addresses are returned
            Assert.Equal("test", validator.GetValidAddresses()[0]);
            Assert.Equal("test 2", validator.GetValidAddresses()[1]);
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
                Endorsement = new Endorsement.Endorsement(new byte[] { 0xAA, 0xAA, 0xAA }, new byte[] { 0xBB, 0xBB, 0XBB }),
                PrivateReadWriteSet = new ReadWriteSet
                {
                    Reads = new List<ReadItem>
                    {
                        new ReadItem { ContractAddress = uint160.One, Key = new byte[] { 0xCC, 0xCC, 0xCC }, Version = "1"}
                    },
                    Writes = new List<WriteItem>
                    {
                        new WriteItem { ContractAddress = uint160.One, IsPrivateData = true, Key = new byte[] { 0xDD, 0xDD, 0xDD }, Value = new byte[] { 0xEE, 0xEE, 0xEE }}
                    }
                }
            };

            var toBytes = proposalResponse.ToBytes();

            var fromBytes = SignedProposalResponse.FromBytes(toBytes);

            // Full roundtrip serialize and compare.
            Assert.True(toBytes.SequenceEqual(fromBytes.ToBytes()));
        }
    }
}

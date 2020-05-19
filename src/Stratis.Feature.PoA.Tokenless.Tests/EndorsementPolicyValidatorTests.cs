using System.Collections.Generic;
using System.IO;
using MembershipServices;
using Moq;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.SmartContracts.Core.Endorsement;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class EndorsementPolicyValidatorTests
    {
        [Fact]
        public void Valid_Policy_With_One_Invalid_Signature_Succeeds()
        {
            var membershipServices = new Mock<IMembershipServicesDirectory>();
            var organisationLookup = new Mock<IOrganisationLookup>();
            var stateRoot = new Mock<IStateRepositoryRoot>();
            var signatureValidator = new Mock<IEndorsementSignatureValidator>();

            var validator = new EndorsementPolicyValidator(membershipServices.Object, organisationLookup.Object, stateRoot.Object, signatureValidator.Object);

            var rws = new ReadWriteSet();
            rws.Reads = new List<ReadItem>();
            // Add a read item so the RWS has a contract address
            rws.Reads.Add(new ReadItem { ContractAddress = uint160.One, Key = new byte[] {}, Version = "1.1" });

            // Create a policy with 2 required signatures
            var policy = new EndorsementPolicy
            {
                Organisation = (Organisation)"TestOrgansation",
                RequiredSignatures = 2
            };

            // Return the policy.
            stateRoot.Setup(x => x.GetPolicy(It.IsAny<uint160>())).Returns(policy);

            var key = new Key();
            var pubKey = key.PubKey.ToBytes();

            // Create an endorsement from two valid signatures and one invalid signature.
            var endorsements = new List<Endorsement.Endorsement>();

            // Doesn't actually matter what the signatures or pubkeys are, the real ones are never used.
            var validEndorsement = new Endorsement.Endorsement(new byte[] {}, pubKey);
            var validEndorsement2 = new Endorsement.Endorsement(new byte[] {}, pubKey);
            var dudEndorsement = new Endorsement.Endorsement(new byte[] {}, pubKey);

            endorsements.Add(validEndorsement);
            endorsements.Add(validEndorsement2);
            endorsements.Add(dudEndorsement);

            var certParser = new X509CertificateParser();

            var dummyCert = certParser.ReadCertificate(File.ReadAllBytes("Certificates/cert.crt"));

            // Don't care what this is as long as it's not null.
            membershipServices.Setup(s => s.GetCertificateForTransactionSigningPubKeyHash(It.IsAny<byte[]>())).Returns(dummyCert);

            // Setup the signature validation state of the endorsements.
            signatureValidator.Setup(s => s.Validate(validEndorsement, It.IsAny<byte[]>())).Returns(true);
            signatureValidator.Setup(s => s.Validate(validEndorsement2, It.IsAny<byte[]>())).Returns(true);
            signatureValidator.Setup(s => s.Validate(dudEndorsement, It.IsAny<byte[]>())).Returns(false);

            SetupSenders(organisationLookup, dummyCert, policy);

            Assert.True(validator.Validate(rws, endorsements));

            // Have to do this again
            SetupSenders(organisationLookup, dummyCert, policy);

            // Try again, with one of the valid signatures removed.
            Assert.False(validator.Validate(rws, new []{ validEndorsement, dudEndorsement }));

            SetupSenders(organisationLookup, dummyCert, policy);

            // Try again, with only the valid signatures.
            Assert.True(validator.Validate(rws, new[] { validEndorsement, validEndorsement2 }));
        }

        private static void SetupSenders(Mock<IOrganisationLookup> organisationLookup, X509Certificate dummyCert, EndorsementPolicy policy)
        {
            organisationLookup.SetupSequence(o => o.FromCertificate(dummyCert))
                .Returns((policy.Organisation, "test address"))
                .Returns((policy.Organisation, "test address 2"))
                .Returns((policy.Organisation, "test address 3"));
        }
    }
}
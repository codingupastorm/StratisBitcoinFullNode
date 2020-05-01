using Moq;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.SmartContracts.Core.Endorsement;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class EndorsementsTests
    {
        private readonly Mock<IOrganisationLookup> lookup;
        private readonly Mock<IEndorsementSignatureValidator> endorsementValidator;

        public EndorsementsTests()
        {
            this.lookup = new Mock<IOrganisationLookup>();
            this.endorsementValidator = new Mock<IEndorsementSignatureValidator>();
        }

        [Fact]
        public void Can_Record_Endorsement()
        {
            var endorsements = new Endorsements(this.lookup.Object, this.endorsementValidator.Object);

            var proposalId = new uint256(RandomUtils.GetBytes(32));

            var endorsementInfo = endorsements.RecordEndorsement(proposalId, new EndorsementPolicy());

            Assert.Same(endorsementInfo, endorsements.GetEndorsement(proposalId));
        }

        [Fact]
        public void Record_Same_ProposalId_ReturnsExisting()
        {
            // Covers the scenario where we receive a proposal response for which an endorsement (from another endorser) has already been recorded.
            var endorsements = new Endorsements(this.lookup.Object, this.endorsementValidator.Object);

            var proposalId = new uint256(RandomUtils.GetBytes(32));

            var endorsementInfo = endorsements.RecordEndorsement(proposalId, new EndorsementPolicy());

            Assert.Same(endorsementInfo, endorsements.RecordEndorsement(proposalId, new EndorsementPolicy()));
        }
    }
}
using System.Linq;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.SmartContracts.Core.Endorsement;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class EndorsementPolicyTests
    {

        [Fact]
        public void CanSerializeAndDeserialize()
        {
            EndorsementPolicy policy = new EndorsementPolicy
            {
                Organisation = (Organisation) "TestOrganisation",
                RequiredSignatures = 3
            };

            byte[] serialized = policy.ToJsonEncodedBytes();

            EndorsementPolicy policy2 = EndorsementPolicy.FromJsonEncodedBytes(serialized);

            Assert.Equal(policy.Organisation, policy2.Organisation);
            Assert.Equal(policy.RequiredSignatures, policy2.RequiredSignatures);
        }

        [Fact]
        public void Empty_ToDictionary_Success()
        {
            Assert.Empty(new EndorsementPolicy().ToDictionary());
        }

        [Fact]
        public void NotEmtpy_ToDictionary_Success()
        {
            var policy = new EndorsementPolicy
            {
                Organisation = (Organisation) "Test",
                RequiredSignatures = 1
            };

            Assert.Single(policy.ToDictionary());
            Assert.Equal(policy.Organisation, policy.ToDictionary().Keys.First());
            Assert.Equal(policy.RequiredSignatures, policy.ToDictionary().Values.First());
        }
    }
}

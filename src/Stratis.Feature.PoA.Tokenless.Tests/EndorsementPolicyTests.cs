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
    }
}

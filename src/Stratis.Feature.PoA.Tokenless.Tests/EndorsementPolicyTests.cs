using System.Collections.Generic;
using System.Linq;
using Stratis.SmartContracts.Core.AccessControl;
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
                AccessList = new AccessControlList
                {
                    Organisations = new List<string>
                    {
                        "TestOrganisation"
                    }
                },
                RequiredSignatures = 3
            };
            byte[] serialized = policy.ToJsonEncodedBytes();

            EndorsementPolicy policy2 = EndorsementPolicy.FromJsonEncodedBytes(serialized);

            Assert.Equal(policy.AccessList.Organisations.First(), policy2.AccessList.Organisations.First());
            Assert.Equal(policy.RequiredSignatures, policy2.RequiredSignatures);
        }
    }
}

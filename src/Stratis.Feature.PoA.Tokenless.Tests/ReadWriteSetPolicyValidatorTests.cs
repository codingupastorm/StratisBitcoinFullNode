using Moq;
using NBitcoin;
using Stratis.Features.PoA.ProtocolEncryption;
using Stratis.SmartContracts.Core.Endorsement;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class ReadWriteSetPolicyValidatorTests
    {
        [Fact]
        public void Organisation_CanAccess_RWS()
        {
            var org = (Organisation) "dummyOrganisation";
            var addr = new uint160(RandomUtils.GetBytes(20));

            var sr = new Mock<IStateRepositoryRoot>();

            // SR returns a policy for our contract fake address.
            sr.Setup(s => s.GetPolicy(addr))
                .Returns(new EndorsementPolicy
                {
                    Organisation = org,
                    RequiredSignatures = 1
                });

            var rwsBuilder = new ReadWriteSetBuilder();

            rwsBuilder.AddWriteItem(new ReadWriteSetKey(addr, new byte[] { 0xAA }), new byte[] {0XBB}, true);
            
            var validator = new ReadWriteSetPolicyValidator(Mock.Of<ICertificatesManager>(), sr.Object);
            
            Assert.True(validator.OrganisationCanAccessPrivateData(org, rwsBuilder.GetReadWriteSet()));
        }

        [Fact]
        public void Organisation_Can_Not_Access_RWS()
        {
            var org = (Organisation)"dummyOrganisation";
            var addr = new uint160(RandomUtils.GetBytes(20));
            var disallowedOrg = (Organisation)"disallowedOrganisation";
            var sr = new Mock<IStateRepositoryRoot>();

            // SR returns a policy for our contract fake address.
            sr.Setup(s => s.GetPolicy(addr))
                .Returns(new EndorsementPolicy
                {
                    Organisation = org,
                    RequiredSignatures = 1
                });

            var rwsBuilder = new ReadWriteSetBuilder();

            rwsBuilder.AddWriteItem(new ReadWriteSetKey(addr, new byte[] { 0xAA }), new byte[] { 0XBB }, true);

            var validator = new ReadWriteSetPolicyValidator(Mock.Of<ICertificatesManager>(), sr.Object);

            Assert.False(validator.OrganisationCanAccessPrivateData(disallowedOrg, rwsBuilder.GetReadWriteSet()));
        }

        [Fact]
        public void Policy_Is_Null_Can_Not_Access_RWS()
        {
            var org = (Organisation)"dummyOrganisation";
            var addr = new uint160(RandomUtils.GetBytes(20));
            var sr = new Mock<IStateRepositoryRoot>();

            // SR returns a policy for our contract fake address.
            sr.Setup(s => s.GetPolicy(addr))
                .Returns((EndorsementPolicy)null);

            var rwsBuilder = new ReadWriteSetBuilder();

            rwsBuilder.AddWriteItem(new ReadWriteSetKey(addr, new byte[] { 0xAA }), new byte[] { 0XBB }, true);

            var validator = new ReadWriteSetPolicyValidator(Mock.Of<ICertificatesManager>(), sr.Object);

            Assert.False(validator.OrganisationCanAccessPrivateData(org, rwsBuilder.GetReadWriteSet()));
        }
    }
}
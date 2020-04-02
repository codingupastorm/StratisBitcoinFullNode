using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Feature.PoA.Tokenless.Config;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class ConfigTests
    {
        [Fact]
        public void Serialize_PrivateDataConfig_RoundTrip_Success()
        {
            var serializer = new PrivateDataConfigSerializer();

            var privateDataConfig = new PrivateDataConfig
            {
                BlockToLive = 1234,
                MaximumPeerCount = 5678,
                MemberOnlyRead = true,
                MemberOnlyWrite = true,
                MinimumPeerCount = 9123,
                Name = "TEST",
                PolicyInfo = new PolicyInfo
                {
                    Policy = "TEST POLICY",
                    PolicyType = PolicyType.Signature
                }
            };

            var result = serializer.Serialize(privateDataConfig);

            var deserialized = serializer.Deserialize(result);

            Assert.Equal(privateDataConfig.Name, deserialized.Name);
            Assert.Equal(privateDataConfig.MemberOnlyRead, deserialized.MemberOnlyRead);
            Assert.Equal(privateDataConfig.MemberOnlyWrite, deserialized.MemberOnlyWrite);
            Assert.Equal(privateDataConfig.MinimumPeerCount, deserialized.MinimumPeerCount);
            Assert.Equal(privateDataConfig.MaximumPeerCount, deserialized.MaximumPeerCount);
            Assert.Equal(privateDataConfig.BlockToLive, deserialized.BlockToLive);
            Assert.Equal(privateDataConfig.PolicyInfo.PolicyType, deserialized.PolicyInfo.PolicyType);
            Assert.Equal(privateDataConfig.PolicyInfo.Policy, deserialized.PolicyInfo.Policy);
        }
    }
}

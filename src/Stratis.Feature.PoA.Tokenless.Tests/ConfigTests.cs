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

            var privateDataConfig = new PrivateDataConfig("TEST",
                new PolicyInfo(PolicyType.Signature, "TEST POLICY"),
                1234,
                5678,
                9123,
                true,
                true);

            var result = serializer.Serialize(privateDataConfig);

            PrivateDataConfig deserialized = serializer.Deserialize(result);

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

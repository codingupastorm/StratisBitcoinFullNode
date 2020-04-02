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

        [Fact]
        public void PrivateDataConfig_Validation_Success()
        {
            var privateDataConfig = new PrivateDataConfig("TEST",
                new PolicyInfo(PolicyType.Signature, "TEST POLICY"),
                1234,
                5678,
                9123,
                true,
                true);

            Assert.True(privateDataConfig.Validate().IsSuccess);
        }

        [Fact]
        public void PrivateDataConfig_Validation_PeerCount()
        {
            var privateDataConfig = new PrivateDataConfig("TEST",
                new PolicyInfo(PolicyType.Signature, "TEST POLICY"),
                1234,
                1000,
                1,
                true,
                true);

            var validationResult = privateDataConfig.Validate();
            Assert.False(validationResult.IsSuccess);
            Assert.Equal(validationResult.Error, PrivateDataConfig.PeerCountError);
        }

        [Fact]
        public void PrivateDataConfig_Validation_MinPeerCount()
        {
            var privateDataConfig = new PrivateDataConfig("TEST",
                new PolicyInfo(PolicyType.Signature, "TEST POLICY"),
                1234,
                -1,
                1,
                true,
                true);

            var validationResult = privateDataConfig.Validate();
            Assert.False(validationResult.IsSuccess);
            Assert.Equal(validationResult.Error, PrivateDataConfig.MinimumPeerCountLessThanZeroError);
        }

        [Fact]
        public void PrivateDataConfig_Validation_Identifier()
        {
            var privateDataConfig = new PrivateDataConfig(" ",
                new PolicyInfo(PolicyType.Signature, "TEST POLICY"),
                1234,
                -1,
                1,
                true,
                true);

            var validationResult = privateDataConfig.Validate();
            Assert.False(validationResult.IsSuccess);
            Assert.Equal(validationResult.Error, PrivateDataConfig.EmptyFieldNameError);

            privateDataConfig = new PrivateDataConfig("INVALID IDENTIFIER",
                new PolicyInfo(PolicyType.Signature, "TEST POLICY"),
                1234,
                -1,
                1,
                true,
                true);

            validationResult = privateDataConfig.Validate();
            Assert.False(validationResult.IsSuccess);
            Assert.Equal(validationResult.Error, PrivateDataConfig.FieldNameError);
        }
    }
}

﻿using System.Text.Json;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public sealed class TokenlessNetworkTests
    {
        [Fact]
        public void CanSerializeAndDeserializeNetwork()
        {
            ChannelNetwork channelNetwork = TokenlessNetwork.CreateChannelNetwork("Test", "TestFolder");

            var serialized = JsonSerializer.Serialize(channelNetwork);
            ChannelNetwork deserialized = JsonSerializer.Deserialize<ChannelNetwork>(serialized);

            Assert.NotNull(deserialized.Genesis);
            Assert.Equal(channelNetwork.Genesis.GetHash(), deserialized.Genesis.GetHash());

            Assert.NotNull(deserialized.Consensus);

            Assert.Equal(channelNetwork.Consensus.CoinType, deserialized.Consensus.CoinType);
            Assert.Equal(channelNetwork.Consensus.DefaultAssumeValid, deserialized.Consensus.DefaultAssumeValid);
            Assert.Equal(channelNetwork.Consensus.MaxReorgLength, deserialized.Consensus.MaxReorgLength);
            Assert.Equal(channelNetwork.Consensus.MinimumChainWork, deserialized.Consensus.MinimumChainWork);

            Assert.Equal(channelNetwork.Consensus.Options.EnforcedMinProtocolVersion, deserialized.Consensus.Options.EnforcedMinProtocolVersion);
            Assert.Equal(channelNetwork.Consensus.Options.EnforceMinProtocolVersionAtBlockHeight, deserialized.Consensus.Options.EnforceMinProtocolVersionAtBlockHeight);
            Assert.Equal(channelNetwork.Consensus.Options.MaxBlockBaseSize, deserialized.Consensus.Options.MaxBlockBaseSize);
            Assert.Equal(channelNetwork.Consensus.Options.MaxBlockSerializedSize, deserialized.Consensus.Options.MaxBlockSerializedSize);
            Assert.Equal(channelNetwork.Consensus.Options.MaxBlockWeight, deserialized.Consensus.Options.MaxBlockWeight);
            Assert.Equal(channelNetwork.Consensus.Options.MaxStandardTxWeight, deserialized.Consensus.Options.MaxStandardTxWeight);
            Assert.Equal(channelNetwork.Consensus.Options.MaxStandardVersion, deserialized.Consensus.Options.MaxStandardVersion);

            Assert.Equal(channelNetwork.DefaultAPIPort, deserialized.DefaultAPIPort);
            Assert.Equal(channelNetwork.DefaultBanTimeSeconds, deserialized.DefaultBanTimeSeconds);
            Assert.Equal(channelNetwork.DefaultConfigFilename, deserialized.DefaultConfigFilename);
            Assert.Equal(channelNetwork.DefaultEnableIpRangeFiltering, deserialized.DefaultEnableIpRangeFiltering);
            Assert.Equal(channelNetwork.DefaultMaxInboundConnections, deserialized.DefaultMaxInboundConnections);
            Assert.Equal(channelNetwork.DefaultMaxOutboundConnections, deserialized.DefaultMaxOutboundConnections);
            Assert.Equal(channelNetwork.DefaultPort, deserialized.DefaultPort);
            Assert.Equal(channelNetwork.DefaultSignalRPort, deserialized.DefaultSignalRPort);
            Assert.Equal(channelNetwork.Magic, deserialized.Magic);
            Assert.Equal(channelNetwork.MaxTimeOffsetSeconds, deserialized.MaxTimeOffsetSeconds);
            Assert.Equal(channelNetwork.MaxTipAge, deserialized.MaxTipAge);
            Assert.Equal(channelNetwork.Name, deserialized.Name);
            Assert.Equal(channelNetwork.NetworkType, deserialized.NetworkType);
            Assert.Equal(channelNetwork.RootFolderName, deserialized.RootFolderName);
        }
    }
}
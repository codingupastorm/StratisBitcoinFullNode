﻿using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Core.Builder;
using Stratis.Core.Builder.Feature;
using Stratis.Core.Configuration;
using Stratis.Core.Networks;
using Xunit;

namespace Stratis.Bitcoin.Tests.Builder
{
    public class FullNodeBuilderExtensionsTest
    {
        private readonly FeatureCollection featureCollection;
        private readonly List<Action<IFeatureCollection>> featureCollectionDelegates;
        private readonly FullNodeBuilder fullNodeBuilder;
        private readonly List<Action<IServiceCollection>> serviceCollectionDelegates;
        private readonly List<Action<IServiceProvider>> serviceProviderDelegates;

        public FullNodeBuilderExtensionsTest()
        {
            this.serviceCollectionDelegates = new List<Action<IServiceCollection>>();
            this.serviceProviderDelegates = new List<Action<IServiceProvider>>();
            this.featureCollectionDelegates = new List<Action<IFeatureCollection>>();
            this.featureCollection = new FeatureCollection();

            this.fullNodeBuilder = new FullNodeBuilder(this.serviceCollectionDelegates, this.serviceProviderDelegates, this.featureCollectionDelegates, this.featureCollection)
            {
                Network = new BitcoinTest()
            };
        }

        [Fact]
        public void UseNodeSettingsConfiguresNodeBuilderWithNodeSettings()
        {
            FullNodeBuilderNodeSettingsExtension.UseDefaultNodeSettings(this.fullNodeBuilder);

            Assert.NotNull(this.fullNodeBuilder.NodeSettings);
            Assert.Equal(NodeSettings.Default(this.fullNodeBuilder.Network).ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
            Assert.Equal(NodeSettings.Default(this.fullNodeBuilder.Network).DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
            Assert.NotNull(this.fullNodeBuilder.Network);
            Assert.Equal(NodeSettings.Default(this.fullNodeBuilder.Network).Network, this.fullNodeBuilder.Network);
            Assert.Single(this.serviceCollectionDelegates);
        }

        [Fact]
        public void UseDefaultNodeSettingsConfiguresNodeBuilderWithDefaultSettings()
        {
            var nodeSettings = new NodeSettings(this.fullNodeBuilder.Network, args: new string[] {
                "-datadir=TestData/FullNodeBuilder/UseNodeSettings" });

            FullNodeBuilderNodeSettingsExtension.UseNodeSettings(this.fullNodeBuilder, nodeSettings);

            Assert.NotNull(this.fullNodeBuilder.NodeSettings);
            Assert.Equal(nodeSettings.ConfigurationFile, this.fullNodeBuilder.NodeSettings.ConfigurationFile);
            Assert.Equal(nodeSettings.DataDir, this.fullNodeBuilder.NodeSettings.DataDir);
            Assert.NotNull(this.fullNodeBuilder.Network);
            Assert.Equal(new BitcoinTest().Name, this.fullNodeBuilder.Network.Name);
            Assert.Single(this.serviceCollectionDelegates);
        }
    }
}

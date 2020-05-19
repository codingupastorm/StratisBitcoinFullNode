﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Core.Configuration;
using Stratis.Core.Utilities;
using Stratis.Feature.PoA.Tokenless.Channels;

namespace Stratis.SmartContracts.Tests.Common
{
    public class TestChannelService : ChannelService
    {
        /// <summary>
        /// This allows us to create nodes within the same process.
        /// This is hacky. If you have ideas for how to better implement this then do so!
        /// </summary>
        public SmartContractNodeBuilder NodeBuilder { get; } 

        public List<CoreNode> ChannelNodes { get; }

        public TestChannelService(
            ChannelSettings channelSettings,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            NodeSettings nodeSettings,
            IChannelRepository channelRepository,
            SmartContractNodeBuilder nodeBuilder) 
            : base(channelSettings, dateTimeProvider, loggerFactory, nodeSettings, channelRepository)
        {
            this.ChannelNodes = new List<CoreNode>();
            this.NodeBuilder = nodeBuilder;
        }

        public override void Initialize()
        {
            // Nothing to do here
        }

        protected override async Task<bool> StartChannelAsync(string channelRootFolder, params string[] channelArgs)
        {
            if (this.NodeBuilder == null)
            {
                throw new InvalidOperationException("If you want to use this test class, you need to retrieve it from the node's services and set the NodeBuilder to be the same NodeBuilder your test is consuming." +
                                                    "Note that using this component may not work with restarting channels yet as we are not able to set the NodeBuilder prior.");
            }

            // Create channel configuration file.
            if (this.nodeSettings.DebugMode)
            {
                this.logger.LogInformation($"Starting daemon in debug mode.");
                channelArgs = channelArgs.Concat(new[] { "-debug=1" }).ToArray();
            }

            CreateChannelConfigurationFile(channelRootFolder, channelArgs.Concat(new[] { "-ischannelnode=true" }).ToArray());

            channelArgs = channelArgs.Concat(new[]
            {
                $"-conf={ChannelConfigurationFileName}",
                $"-datadir={channelRootFolder}"
            }).ToArray();

            CoreNode node = this.NodeBuilder.CreateChannelNode(channelRootFolder, channelArgs);

            node.Start();

            this.ChannelNodes.Add(node);

            return true;
        }

        public override void StopChannelNodes()
        {
            // Because the nodes are created by the NodeBuilder, we can let it clean them up.
        }
    }
}

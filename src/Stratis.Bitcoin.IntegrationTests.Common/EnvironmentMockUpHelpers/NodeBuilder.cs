using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NBitcoin;
using NBitcoin.Protocol;
using NLog;
using Stratis.Core.Builder;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    public class NodeBuilder : IDisposable
    {
        public List<CoreNode> Nodes { get; }

        public NodeConfigParameters ConfigParameters { get; }

        protected readonly string rootFolder;

        public NodeBuilder(string rootFolder)
        {
            this.Nodes = new List<CoreNode>();
            this.ConfigParameters = new NodeConfigParameters();

            this.rootFolder = rootFolder;
        }

        public static NodeBuilder Create(object caller, [CallerMemberName] string callingMethod = null)
        {
            string testFolderPath = TestBase.CreateTestDir(caller, callingMethod);
            return CreateNodeBuilder(testFolderPath);
        }

        public static NodeBuilder Create(string testDirectory)
        {
            string testFolderPath = TestBase.CreateTestDir(testDirectory);
            return CreateNodeBuilder(testFolderPath);
        }

        /// <summary>
        /// Creates a node builder instance and disable logs.
        /// To enable logs please refer to the <see cref="WithLogsEnabled"/> method.
        /// </summary>
        /// <param name="testFolderPath">The test folder path.</param>
        /// <returns>A <see cref="NodeBuilder"/> instance with logs disabled.</returns>
        private static NodeBuilder CreateNodeBuilder(string testFolderPath)
        {
            return new NodeBuilder(testFolderPath)
                .WithLogsDisabled();
        }

        protected CoreNode CreateNode(NodeRunner runner, string configFile = "bitcoin.conf", NodeConfigParameters configParameters = null)
        {
            var node = new CoreNode(runner, configParameters, configFile);
            this.Nodes.Add(node);
            return node;
        }

        /// <summary>A helper method to create a node instance with a non-standard set of features enabled. The node can be PoW or PoS, as long as the appropriate features are provided.</summary>
        /// <param name="callback">A callback accepting an instance of <see cref="IFullNodeBuilder"/> that constructs a node with a custom feature set.</param>
        /// <param name="network">The network the node will be running on.</param>
        /// <param name="protocolVersion">Use <see cref="ProtocolVersion.PROTOCOL_VERSION"/> for BTC PoW-like networks and <see cref="ProtocolVersion.ALT_PROTOCOL_VERSION"/> for Stratis PoS-like networks.</param>
        /// <param name="agent">A user agent string to distinguish different node versions from each other.</param>
        /// <param name="configParameters">Use this to pass in any custom configuration parameters used to set up the CoreNode</param>
        public CoreNode CreateCustomNode(Action<IFullNodeBuilder> callback, Network network, ProtocolVersion protocolVersion = ProtocolVersion.PROTOCOL_VERSION, string agent = "Custom", NodeConfigParameters configParameters = null, ProtocolVersion minProtocolVersion = ProtocolVersion.PROTOCOL_VERSION)
        {
            configParameters = configParameters ?? new NodeConfigParameters();

            configParameters.SetDefaultValueIfUndefined("conf", "custom.conf");
            string configFileName = configParameters["conf"];

            configParameters.SetDefaultValueIfUndefined("datadir", this.GetNextDataFolderName(agent));
            string dataDir = configParameters["datadir"];

            configParameters.ToList().ForEach(p => this.ConfigParameters[p.Key] = p.Value);
            return CreateNode(new CustomNodeRunner(dataDir, callback, network, protocolVersion, configParameters, agent, minProtocolVersion), configFileName);
        }

        protected string GetNextDataFolderName(string folderName = null, int? nodeIndex = null)
        {
            string hash = nodeIndex?.ToString() ?? Guid.NewGuid().ToString("N").Substring(0, 7);
            string numberedFolderName = string.Join(
                ".",
                new[] { hash, folderName }.Where(s => s != null));

            string dataFolderName = Path.Combine(this.rootFolder, numberedFolderName);
            return dataFolderName;
        }

        public void Dispose()
        {
            foreach (CoreNode node in this.Nodes)
                node.Kill();

            // Logs are static so clear them after every run.
            LogManager.Configuration.LoggingRules.Clear();
            LogManager.ReconfigExistingLoggers();
        }

        /// <summary>
        /// By default, logs are disabled when using <see cref="Create(string)"/> or <see cref="Create(object, string)"/> methods,
        /// by using this fluent method the caller can enable the logs at will.
        /// </summary>
        /// <returns>Current <see cref="NodeBuilder"/> instance, used for fluent API style</returns>
        /// <example>
        /// //default use (without logs)
        /// using (NodeBuilder builder = NodeBuilder.Create(this))
        /// {
        ///     //your test code here
        /// }
        ///
        /// //with logs enabled
        /// using (NodeBuilder builder = NodeBuilder.Create(this).WithLogsEnabled())
        /// {
        ///     //your test code here
        /// }
        /// </example>
        public NodeBuilder WithLogsEnabled()
        {
            // NLog Enable/Disable logging is based on internal counter. To ensure logs are enabled
            // keep calling EnableLogging until IsLoggingEnabled returns true.
            while (!LogManager.IsLoggingEnabled())
            {
                LogManager.EnableLogging();
            }

            return this;
        }

        /// <summary>
        /// If logs have been enabled by calling WithLogsEnabled you can disable it manually by calling this method.
        /// If the test is running within an "using block" where the nodebuilder is created, without using <see cref="WithLogsEnabled"/>,
        /// you shouldn't need to call this method.
        /// </summary>
        /// <returns>Current <see cref="NodeBuilder"/> instance, used for fluent API style.</returns>
        public NodeBuilder WithLogsDisabled()
        {
            // NLog Enable/Disable logging is based on internal counter. To ensure logs are disabled
            // keep calling DisableLogging until IsLoggingEnabled returns false.
            while (LogManager.IsLoggingEnabled())
            {
                LogManager.DisableLogging();
            }

            return this;
        }
    }
}
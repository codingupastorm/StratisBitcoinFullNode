using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CertificateAuthority.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.AsyncWork;
using Stratis.Features.Wallet;
using Stratis.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    public class CoreNode
    {
        private readonly object lockObject = new object();
        private readonly ILoggerFactory loggerFactory;
        internal readonly NodeRunner runner;

        public int ApiPort => int.Parse(this.ConfigParameters["apiport"]);

        public BitcoinSecret MinerSecret { get; private set; }

        public HdAddress MinerHDAddress { get; internal set; }
        public int ProtocolPort => int.Parse(this.ConfigParameters["port"]);

        /// <summary>Location of the data directory for the node.</summary>
        public string DataFolder => this.runner.DataFolder;

        public IPEndPoint Endpoint => new IPEndPoint(IPAddress.Parse("127.0.0.1"), this.ProtocolPort);

        public string ConfigFilePath { get; }

        public NodeConfigParameters ConfigParameters { get; set; }

        public Mnemonic Mnemonic { get; set; }

        public DateTimeOffset? MockTime { get; set; }

        public CertificateInfoModel ClientCertificate { get; set; }

        public FullNode FullNode => this.runner.FullNode;

        public CoreNodeState State { get; private set; }

        private bool builderAlwaysFlushBlocks;
        private bool builderEnablePeerDiscovery;
        private bool builderNoValidation;
        private bool builderOverrideDateTimeProvider;
        private bool builderWithDummyWallet;
        private bool builderWithWallet;
        private string builderWalletName;
        private string builderWalletPassword;
        private string builderWalletPassphrase;
        private string builderWalletMnemonic;

        private SubscriptionToken blockConnectedSubscription;
        private SubscriptionToken blockDisconnectedSubscription;

        List<Action> startActions = new List<Action>();
        List<Action> runActions = new List<Action>();

        public Key ClientCertificatePrivateKey { get; set; }
        public Key TransactionSigningPrivateKey { get; set; }

        public CoreNode(NodeRunner runner, NodeConfigParameters configParameters, string configfile)
        {
            this.runner = runner;

            this.State = CoreNodeState.Stopped;
            this.ConfigFilePath = Path.Combine(this.runner.DataFolder, configfile);

            this.ConfigParameters = new NodeConfigParameters();
            if (configParameters != null)
                this.ConfigParameters.Import(configParameters);

            // Set the various ports.
            var randomFoundPorts = new int[2];
            IpHelper.FindPorts(randomFoundPorts);
            this.ConfigParameters.SetDefaultValueIfUndefined("port", randomFoundPorts[0].ToString());
            this.ConfigParameters.SetDefaultValueIfUndefined("apiport", randomFoundPorts[1].ToString());

            this.loggerFactory = new ExtendedLoggerFactory();

            CreateConfigFile(this.ConfigParameters);
        }

        public CoreNode NoValidation()
        {
            this.builderNoValidation = true;
            return this;
        }

        /// <summary>
        /// Executes a function when a block has connected.
        /// </summary>
        /// <param name="interceptor">A function that is called everytime a block connects.</param>
        /// <returns>This node.</returns>
        public CoreNode SetConnectInterceptor(Action<ChainedHeaderBlock> interceptor)
        {
            this.blockConnectedSubscription = this.FullNode.NodeService<ISignals>().Subscribe<BlockConnected>(ev => interceptor(ev.ConnectedBlock));

            return this;
        }

        /// <summary>
        /// Executes a function when a block has disconnected.
        /// </summary>
        /// <param name="interceptor">A function that is called when a block disconnects.</param>
        /// <returns>This node.</returns>
        public CoreNode SetDisconnectInterceptor(Action<ChainedHeaderBlock> interceptor)
        {
            this.blockDisconnectedSubscription = this.FullNode.NodeService<ISignals>().Subscribe<BlockDisconnected>(ev => interceptor(ev.DisconnectedBlock));

            return this;
        }

        /// <summary>
        /// Enables <see cref="PeerDiscovery"/> and <see cref="PeerConnectorDiscovery"/> which is disabled by default.
        /// </summary>
        /// <returns>This node.</returns>
        public CoreNode EnablePeerDiscovery()
        {
            this.builderEnablePeerDiscovery = true;
            return this;
        }

        public CoreNode AlwaysFlushBlocks()
        {
            this.builderAlwaysFlushBlocks = true;
            return this;
        }

        /// <summary>
        /// Overrides the node's date time provider with one where the current date time starts 2018-01-01.
        /// <para>
        /// This is primarily used where we want to mine coins in the past used for staking.
        /// </para>
        /// </summary>
        /// <returns>This node.</returns>
        public CoreNode OverrideDateTimeProvider()
        {
            this.builderOverrideDateTimeProvider = true;
            return this;
        }

        /// <summary>
        /// Overrides a node service.
        /// </summary>
        /// <param name="serviceToOverride">A function that will override a given service in the node.</param>
        /// <returns>This node.</returns>
        public CoreNode OverrideService(Action<IServiceCollection> serviceToOverride)
        {
            this.runner.ServiceToOverride = serviceToOverride;
            return this;
        }

        /// <summary>
        /// This does not create a physical wallet but only sets the miner secret on the node.
        /// </summary>
        /// <returns>This node.</returns>
        public CoreNode WithDummyWallet()
        {
            this.builderWithDummyWallet = true;
            this.builderWithWallet = false;
            return this;
        }

        /// <summary>
        /// Adds a wallet to this node with defaulted parameters.
        /// </summary>
        /// <param name="walletPassword">Wallet password defaulted to "password".</param>
        /// <param name="walletName">Wallet name defaulted to "mywallet".</param>
        /// <param name="walletPassphrase">Wallet passphrase defaulted to "passphrase".</param>
        /// <param name="walletMnemonic">Optional wallet mnemonic.</param>
        /// <returns>This node.</returns>
        public CoreNode WithWallet(string walletPassword = "password", string walletName = "mywallet", string walletPassphrase = "passphrase", string walletMnemonic = null)
        {
            this.builderWithDummyWallet = false;
            this.builderWithWallet = true;
            this.builderWalletName = walletName;
            this.builderWalletPassphrase = walletPassphrase;
            this.builderWalletPassword = walletPassword;
            this.builderWalletMnemonic = walletMnemonic;
            return this;
        }

        public CoreNode WithReadyBlockchainData(string readyDataName)
        {
            // Extract the zipped blockchain data to the node's DataFolder.
            ZipFile.ExtractToDirectory(Path.GetFullPath(readyDataName), this.DataFolder, true);

            // Import whole wallets to DB.
            this.startActions.Add(() =>
            {
                var walletManager = ((WalletManager)this.FullNode?.NodeService<IWalletManager>(true));
                if (walletManager != null)
                    walletManager.ExcludeTransactionsFromWalletImports = false;
            });

            return this;
        }

        public INetworkPeer CreateNetworkPeerClient()
        {
            ConnectionManagerSettings connectionManagerSettings = this.runner.FullNode.ConnectionManager.ConnectionSettings;

            var selfEndPointTracker = new SelfEndpointTracker(this.loggerFactory, connectionManagerSettings);

            // Needs to be initialized beforehand.
            selfEndPointTracker.UpdateAndAssignMyExternalAddress(new IPEndPoint(IPAddress.Parse("0.0.0.0").MapToIPv6Ex(), this.ProtocolPort), false);

            var ibdState = new Mock<IInitialBlockDownloadState>();
            ibdState.Setup(x => x.IsInitialBlockDownload()).Returns(() => true);

            var peerAddressManager = new Mock<IPeerAddressManager>().Object;

            var networkPeerFactory = new NetworkPeerFactory(this.runner.Network,
                DateTimeProvider.Default,
                this.loggerFactory,
                new PayloadProvider().DiscoverPayloads(),
                selfEndPointTracker,
                ibdState.Object,
                connectionManagerSettings,
                this.GetOrCreateAsyncProvider(),
                peerAddressManager);

            return networkPeerFactory.CreateConnectedNetworkPeerAsync("127.0.0.1:" + this.ProtocolPort).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private IAsyncProvider GetOrCreateAsyncProvider()
        {
            if (this.runner.FullNode == null)
                return new AsyncProvider(this.loggerFactory, new Signals.Signals(this.loggerFactory, null), new NodeLifetime());
            else
                return this.runner.FullNode.NodeService<IAsyncProvider>();
        }

        public CoreNode Start(Action startAction = null)
        {
            lock (this.lockObject)
            {
                this.runner.AlwaysFlushBlocks = this.builderAlwaysFlushBlocks;
                this.runner.EnablePeerDiscovery = this.builderEnablePeerDiscovery;
                this.runner.OverrideDateTimeProvider = this.builderOverrideDateTimeProvider;

                if (this.builderNoValidation)
                    this.DisableValidation();

                this.runner.BuildNode();

                startAction?.Invoke();
                foreach (Action action in this.startActions)
                    action.Invoke();

                this.runner.Start();
                this.State = CoreNodeState.Starting;
            }

            StartStratisRunner();

            this.State = CoreNodeState.Running;

            foreach (Action runAction in this.runActions)
                runAction.Invoke();

            return this;
        }

        private void CreateConfigFile(NodeConfigParameters configParameters = null)
        {
            Directory.CreateDirectory(this.runner.DataFolder);

            configParameters = configParameters ?? new NodeConfigParameters();
            configParameters.SetDefaultValueIfUndefined("regtest", "1");
            configParameters.SetDefaultValueIfUndefined("server", "1");
            configParameters.SetDefaultValueIfUndefined("txindex", "1");
            configParameters.SetDefaultValueIfUndefined("printtoconsole", "1");
            configParameters.SetDefaultValueIfUndefined("agentprefix", "node" + this.ProtocolPort);
            configParameters.Import(this.ConfigParameters);

            File.WriteAllText(this.ConfigFilePath, configParameters.ToString());
        }

        public void Restart()
        {
            this.Kill();
            this.Start();
        }

        private void StartStratisRunner()
        {
            var timeToNodeInit = TimeSpan.FromMinutes(1);
            var timeToNodeStart = TimeSpan.FromMinutes(1);

            TestBase.WaitLoop(() => this.runner.FullNode != null,
                cancellationToken: new CancellationTokenSource(timeToNodeInit).Token,
                failureReason: $"Failed to assign instance of FullNode within {timeToNodeInit}");

            TestBase.WaitLoop(() => this.runner.FullNode.State == FullNodeState.Started,
                cancellationToken: new CancellationTokenSource(timeToNodeStart).Token,
                failureReason: $"Failed to achieve state = started within {timeToNodeStart}");

            if (this.builderWithDummyWallet)
                this.SetMinerSecret(new BitcoinSecret(new Key(), this.FullNode.Network));

            if (this.builderWithWallet)
            {
                (_, this.Mnemonic) = this.FullNode.WalletManager().CreateWallet(
                    this.builderWalletPassword,
                    this.builderWalletName,
                    this.builderWalletPassphrase,
                    string.IsNullOrEmpty(this.builderWalletMnemonic) ? null : new Mnemonic(this.builderWalletMnemonic));
            }
        }

        /// <summary>
        /// Clears all consensus rules for this node.
        /// </summary>
        public void DisableValidation()
        {
            this.runner.Network.Consensus.ConsensusRules.FullValidationRules.Clear();
            this.runner.Network.Consensus.ConsensusRules.HeaderValidationRules.Clear();
            this.runner.Network.Consensus.ConsensusRules.IntegrityValidationRules.Clear();
            this.runner.Network.Consensus.ConsensusRules.PartialValidationRules.Clear();
        }

        public void Broadcast(Transaction transaction)
        {
            using (INetworkPeer peer = this.CreateNetworkPeerClient())
            {
                peer.VersionHandshakeAsync().GetAwaiter().GetResult();
                peer.SendMessageAsync(new InvPayload(transaction)).GetAwaiter().GetResult();
                peer.SendMessageAsync(new TxPayload(transaction)).GetAwaiter().GetResult();
                this.PingPongAsync(peer).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Emit a ping and wait the pong.
        /// </summary>
        /// <param name="cancellation"></param>
        /// <param name="peer"></param>
        /// <returns>Latency.</returns>
        public async Task<TimeSpan> PingPongAsync(INetworkPeer peer, CancellationToken cancellation = default(CancellationToken))
        {
            using (var listener = new NetworkPeerListener(peer, this.GetOrCreateAsyncProvider()))
            {
                var ping = new PingPayload()
                {
                    Nonce = RandomUtils.GetUInt64()
                };

                DateTimeOffset before = DateTimeOffset.UtcNow;
                await peer.SendMessageAsync(ping, cancellation).ConfigureAwait(false);

                while ((await listener.ReceivePayloadAsync<PongPayload>(cancellation).ConfigureAwait(false)).Nonce != ping.Nonce)
                {
                }

                DateTimeOffset after = DateTimeOffset.UtcNow;

                return after - before;
            }
        }

        public void Kill()
        {
            lock (this.lockObject)
            {
                this.runner.Stop();

                if (!this.runner.IsDisposed)
                {
                    throw new Exception($"Problem disposing of a node of type {this.runner.GetType()}.");
                }

                this.State = CoreNodeState.Killed;
            }
        }

        public void SetMinerSecret(BitcoinSecret secret)
        {
            this.MinerSecret = secret;
        }

        public ChainedHeader GetTip()
        {
            return this.FullNode.NodeService<IConsensusManager>().Tip;
        }
    }
}
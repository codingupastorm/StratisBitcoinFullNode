using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.Configuration;
using Stratis.Core.Networks;
using Stratis.Core.Utilities;
using Stratis.Features.Consensus;
using Stratis.Features.Consensus.CoinViews;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class NodeContext : IDisposable
    {
        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        private readonly List<IDisposable> cleanList;

        private readonly DBCoinViewStore dBCoinViewStore;

        public NodeContext(object caller, string name, Network network, bool clean)
        {
            network = network ?? new BitcoinRegTest();
            this.loggerFactory = new LoggerFactory();
            this.Network = network;
            this.FolderName = TestBase.CreateTestDir(caller, name);
            var dateTimeProvider = new DateTimeProvider();
            var serializer = new RepositorySerializer(this.Network.Consensus.ConsensusFactory);
            this.dBCoinViewStore = new DBCoinViewStore(serializer, new DataFolder(this.FolderName), this.loggerFactory, dateTimeProvider);
            this.PersistentCoinView = new DBCoinView(network, this.dBCoinViewStore, dateTimeProvider, this.loggerFactory, new NodeStats(dateTimeProvider, this.loggerFactory), serializer);
            this.PersistentCoinView.Initialize();
            this.cleanList = new List<IDisposable> { this.PersistentCoinView };
        }

        public Network Network { get; }

        private ChainBuilder chainBuilder;

        public ChainBuilder ChainBuilder
        {
            get
            {
                return this.chainBuilder = this.chainBuilder ?? new ChainBuilder(this.Network);
            }
        }

        public DBCoinView PersistentCoinView { get; private set; }

        public string FolderName { get; }

        public static NodeContext Create(object caller, [CallerMemberName]string name = null, Network network = null, bool clean = true)
        {
            return new NodeContext(caller, name, network, clean);
        }

        public void Dispose()
        {
            foreach (IDisposable item in this.cleanList)
                item.Dispose();
        }

        public void ReloadPersistentCoinView()
        {
            this.PersistentCoinView.Dispose();
            this.dBCoinViewStore.Dispose();
            this.cleanList.Remove(this.PersistentCoinView);
            var dateTimeProvider = new DateTimeProvider();
            var serializer = new RepositorySerializer(this.Network.Consensus.ConsensusFactory);
            var keyValueStore = new DBCoinViewStore(serializer, new DataFolder(this.FolderName), this.loggerFactory, dateTimeProvider);
            this.PersistentCoinView = new DBCoinView(this.Network, keyValueStore, dateTimeProvider, this.loggerFactory, new NodeStats(dateTimeProvider, this.loggerFactory), serializer);
            this.PersistentCoinView.Initialize();
            this.cleanList.Add(this.PersistentCoinView);
        }
    }
}
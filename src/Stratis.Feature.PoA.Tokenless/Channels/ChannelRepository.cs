﻿using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public interface IChannelRepository
    {
        void Initialize();

        void SaveChannelDefinition(ChannelDefinition request);

        Dictionary<string, ChannelDefinition> GetChannelDefinitions();
    }

    public class ChannelRepository : IChannelRepository
    {
        internal const string ConfigTxTableName = "ConfigTx";

        public IChannelKeyValueStore KeyValueStore { get; }

        private readonly ILogger logger;

        internal readonly Network network;

        private readonly IRepositorySerializer repositorySerializer;

        public ChannelRepository(Network network, ILoggerFactory loggerFactory, IChannelKeyValueStore blockKeyValueStore, IRepositorySerializer repositorySerializer)
        {
            Guard.NotNull(network, nameof(network));

            this.KeyValueStore = blockKeyValueStore;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.network = network;
            this.repositorySerializer = repositorySerializer;
        }

        /// <inheritdoc />
        public void Initialize()
        {
        }

        public void SaveChannelDefinition(ChannelDefinition request)
        {
            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(Bitcoin.Interfaces.KeyValueStoreTransactionMode.ReadWrite, ConfigTxTableName))
            {
                transaction.Insert(ConfigTxTableName, request.Name, request);
                transaction.Commit();
            }
        }

        public Dictionary<string, ChannelDefinition> GetChannelDefinitions()
        {
            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(Bitcoin.Interfaces.KeyValueStoreTransactionMode.Read, ConfigTxTableName))
            {
                return transaction.SelectDictionary<string, ChannelDefinition>(ConfigTxTableName);
            }
        }
    }
}

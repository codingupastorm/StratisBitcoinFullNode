﻿using System.Collections.Generic;
using System.Linq;
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

        void SaveMemberDefinition(ChannelMemberDefinition request);

        Dictionary<string, ChannelDefinition> GetChannelDefinitions();

        Dictionary<string, ChannelMemberDefinition> GetMemberDefinitions(string channelName);

        int GetNextChannelId();
    }

    public class ChannelRepository : IChannelRepository
    {
        internal const string ConfigTxTableName = "ConfigTx";
        internal const string MemberTableNamePrefix = "Member";

        public IChannelKeyValueStore KeyValueStore { get; }

        private readonly ILogger logger;

        internal readonly Network network;

        private readonly IRepositorySerializer repositorySerializer;

        private int maxChannelId = 0;

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
            Dictionary<string, ChannelDefinition> channels = this.GetChannelDefinitions();

            this.maxChannelId = (channels.Count == 0) ? 1 : channels.Values.Max(d => d.Id);
        }

        public int GetNextChannelId()
        {
            return this.maxChannelId + 1;
        }

        public void SaveChannelDefinition(ChannelDefinition request)
        {
            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(Bitcoin.Interfaces.KeyValueStoreTransactionMode.ReadWrite, ConfigTxTableName))
            {
                transaction.Insert(ConfigTxTableName, request.Name, request);
                transaction.Commit();

                if (request.Id > this.maxChannelId)
                    this.maxChannelId = request.Id;
            }
        }

        public void SaveMemberDefinition(ChannelMemberDefinition request)
        {
            string memberTableName = $"{MemberTableNamePrefix}_{request.ChannelName}";

            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(Bitcoin.Interfaces.KeyValueStoreTransactionMode.ReadWrite, memberTableName))
            {
                transaction.Insert(memberTableName, request.MemberPublicKey, request);
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

        public Dictionary<string, ChannelMemberDefinition> GetMemberDefinitions(string channelName)
        {
            string memberTableName = $"{MemberTableNamePrefix}_{channelName}";

            using (IKeyValueStoreTransaction transaction = this.KeyValueStore.CreateTransaction(Bitcoin.Interfaces.KeyValueStoreTransactionMode.Read, memberTableName))
            {
                return transaction.SelectDictionary<string, ChannelMemberDefinition>(memberTableName);
            }
        }
    }
}

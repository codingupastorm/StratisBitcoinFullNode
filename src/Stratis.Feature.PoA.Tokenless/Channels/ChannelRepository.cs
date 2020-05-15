using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Stratis.Core.Interfaces;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public interface IChannelRepository
    {
        void Initialize();

        void SaveChannelDefinition(ChannelDefinition request);

        void SaveMemberDefinition(ChannelMemberDefinition request);

        ChannelDefinition GetChannelDefinition(string channelName);

        Dictionary<string, ChannelDefinition> GetChannelDefinitions();

        Dictionary<string, ChannelMemberDefinition> GetMemberDefinitions(string channelName);

        int GetNextChannelId();
    }

    public sealed class ChannelRepository : IChannelRepository
    {
        internal const string ConfigTxTableName = "ConfigTx";
        internal const string MemberTableNamePrefix = "Member";

        private readonly IChannelKeyValueStore keyValueStore;
        private readonly ILogger logger;

        /// <summary>
        /// The system channel node's id will always be offset by 1 so start here.
        /// </summary>
        private int maxChannelId = 1;

        public ChannelRepository(ILoggerFactory loggerFactory, IChannelKeyValueStore blockKeyValueStore)
        {
            this.keyValueStore = blockKeyValueStore;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public void Initialize()
        {
            Dictionary<string, ChannelDefinition> channels = this.GetChannelDefinitions();

            this.maxChannelId = (channels.Count == 0) ? 1 : channels.Values.Max(d => d.Id);
        }

        /// <inheritdoc />
        public int GetNextChannelId()
        {
            return this.maxChannelId + 1;
        }

        /// <inheritdoc />
        public void SaveChannelDefinition(ChannelDefinition request)
        {
            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, ConfigTxTableName))
            {
                transaction.Insert(ConfigTxTableName, request.Name, request);
                transaction.Commit();

                this.logger.LogDebug($"Channel definition '{request.Name}' saved'.");

                if (request.Id > this.maxChannelId)
                {
                    this.maxChannelId = request.Id;
                    this.logger.LogDebug($"Max channel id set to '{this.maxChannelId}'.");
                }
            }
        }

        /// <inheritdoc />
        public void SaveMemberDefinition(ChannelMemberDefinition request)
        {
            string memberTableName = $"{MemberTableNamePrefix}_{request.ChannelName}";

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, memberTableName))
            {
                transaction.Insert(memberTableName, request.MemberPublicKey, request);
                transaction.Commit();
            }
        }

        /// <inheritdoc />
        public ChannelDefinition GetChannelDefinition(string channelName)
        {
            using IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read, ConfigTxTableName);

            if (transaction.Select(ConfigTxTableName, channelName, out ChannelDefinition channelDefinition))
                return channelDefinition;

            this.logger.LogDebug($"'{channelName}' does not exist.");

            return null;
        }

        /// <inheritdoc />
        public Dictionary<string, ChannelDefinition> GetChannelDefinitions()
        {
            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read, ConfigTxTableName))
            {
                return transaction.SelectDictionary<string, ChannelDefinition>(ConfigTxTableName);
            }
        }

        /// <inheritdoc />
        public Dictionary<string, ChannelMemberDefinition> GetMemberDefinitions(string channelName)
        {
            string memberTableName = $"{MemberTableNamePrefix}_{channelName}";

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read, memberTableName))
            {
                return transaction.SelectDictionary<string, ChannelMemberDefinition>(memberTableName);
            }
        }
    }
}

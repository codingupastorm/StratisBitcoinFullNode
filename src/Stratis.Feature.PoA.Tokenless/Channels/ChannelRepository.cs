using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Interfaces;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public interface IChannelRepository
    {
        void Initialize();

        void SaveChannelDefinition(ChannelDefinition request);

        Dictionary<string, ChannelDefinition> GetChannelDefinitions();

        int GetNextChannelId();
    }

    public sealed class ChannelRepository : IChannelRepository
    {
        private const string ConfigTxTableName = "ConfigTx";

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

                this.logger.LogDebug($"Channel definition '{request.Name}' saved with id '{request.Id}'.");

                if (request.Id > this.maxChannelId)
                {
                    this.maxChannelId = request.Id;
                    this.logger.LogDebug($"Max channel id set to '{this.maxChannelId}'.");
                }
            }
        }

        /// <inheritdoc />
        public Dictionary<string, ChannelDefinition> GetChannelDefinitions()
        {
            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read, ConfigTxTableName))
            {
                return transaction.SelectDictionary<string, ChannelDefinition>(ConfigTxTableName);
            }
        }
    }
}

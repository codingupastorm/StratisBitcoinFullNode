﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.NodeStorage.Interfaces;
using Stratis.Features.NodeStorage.KeyValueStore;
using Stratis.Features.NodeStorage.KeyValueStoreLevelDB;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockStoreFactory
    {
        IKeyValueStore GetStore();
    }

    public class BlockStoreFactory : IBlockStoreFactory
    {
        private readonly DataFolder dataFolder;
        private readonly ILoggerFactory loggerFactory;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly IRepositorySerializer repositorySerializer;

        private KeyValueStore<KeyValueStoreLevelDB> keyValueStore;

        public BlockStoreFactory(Network network, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
        {
            this.repositorySerializer = new DBreezeSerializer(network.Consensus.ConsensusFactory);
            this.dataFolder = dataFolder;
            this.loggerFactory = loggerFactory;
            this.dateTimeProvider = dateTimeProvider;
            this.keyValueStore = null;
        }

        public IKeyValueStore GetStore()
        {
            if (this.keyValueStore == null)
                this.keyValueStore = new KeyValueStore<KeyValueStoreLevelDB>(this.dataFolder.BlockPath, this.loggerFactory, this.dateTimeProvider, this.repositorySerializer);

            return this.keyValueStore;
        }
    }
}

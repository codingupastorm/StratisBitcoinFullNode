using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.NodeStorage.Interfaces;
using Stratis.Features.NodeStorage.KeyValueStore;
using Stratis.Features.NodeStorage.KeyValueStoreLDB;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockStoreFactory
    {
        IKeyValueStore GetStore();
    }

    public class BlockStoreFactory : IBlockStoreFactory
    {
        private DataFolder dataFolder;
        private ILoggerFactory loggerFactory;
        private IDateTimeProvider dateTimeProvider;
        private IRepositorySerializer repositorySerializer;
        private KeyValueStore<KeyValueStoreLDBRepository> keyValueStore;

        public BlockStoreFactory(Network network, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
        {
            this.repositorySerializer = new DBreezeSerializer(network.Consensus.ConsensusFactory);
            this.dataFolder = dataFolder;
            this.loggerFactory = loggerFactory;
            this.dateTimeProvider = dateTimeProvider;
        }

        public IKeyValueStore GetStore()
        {
            if (this.keyValueStore == null)
                this.keyValueStore = new KeyValueStore<KeyValueStoreLDBRepository>(this.dataFolder.BlockPath, this.loggerFactory, this.dateTimeProvider, this.repositorySerializer);

            return this.keyValueStore;
        }
    }
}

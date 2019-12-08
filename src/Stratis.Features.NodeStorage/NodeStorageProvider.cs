using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.NodeStorage.Interfaces;

namespace Stratis.Features.NodeStorage
{
    public class NodeStorageProvider : INodeStorageProvider
    {
        public DataFolder DataFolder { get; private set; }
        public ILoggerFactory LoggerFactory { get; private set; }
        public IDateTimeProvider DateTimeProvider { get; private set; }

        private Dictionary<string, Func<NodeStorageProvider, IRepositorySerializer, IKeyValueStore>> storeProviders;
        private Dictionary<string, IRepositorySerializer> repositorySerializers;
        private Dictionary<string, IKeyValueStore> keyValueStores;

        public NodeStorageProvider(string folder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : this(new DataFolder(folder), loggerFactory, dateTimeProvider)
        {
        }

        public NodeStorageProvider(DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
        {
            this.DataFolder = dataFolder;
            this.LoggerFactory = loggerFactory;
            this.DateTimeProvider = dateTimeProvider;
            this.keyValueStores = new Dictionary<string, IKeyValueStore>();
            this.storeProviders = new Dictionary<string, Func<NodeStorageProvider, IRepositorySerializer, IKeyValueStore>>();
            this.repositorySerializers = new Dictionary<string, IRepositorySerializer>();
        }

        public IKeyValueStore GetStore(string name)
        {
            if (this.keyValueStores.TryGetValue(name, out IKeyValueStore store))
                return store;

            if (!this.storeProviders.TryGetValue(name, out Func<NodeStorageProvider, IRepositorySerializer, IKeyValueStore> creator))
                return null;

            store = creator.Invoke(this, this.repositorySerializers[name]);

            this.keyValueStores[name] = store;

            return store;
        }

        public void RegisterStoreProvider(string name, Func<NodeStorageProvider, IRepositorySerializer, IKeyValueStore> creator, IRepositorySerializer repositorySerializer)
        {
            if (!this.storeProviders.ContainsKey(name))
            {
                this.storeProviders[name] = creator;
                this.repositorySerializers[name] = repositorySerializer;
            }
        }
    }
}

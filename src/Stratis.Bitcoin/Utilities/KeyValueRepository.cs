using System;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Bitcoin.Utilities
{
    public interface IKeyValueRepositoryStore : IKeyValueStore
    {
    }

    public class KeyValueRepositoryStore : KeyValueStore<KeyValueStoreLevelDB.KeyValueStoreLevelDB>, IKeyValueRepositoryStore
    {
        public KeyValueRepositoryStore(Network network, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.KeyValueRepositoryPath, loggerFactory, dateTimeProvider, new DBreezeSerializer(network.Consensus.ConsensusFactory))
        {
        }
    }

    /// <summary>Allows saving and loading single values to and from key-value storage.</summary>
    public interface IKeyValueRepository : IDisposable
    {
        /// <summary>Persists byte array to the database.</summary>
        void SaveBytes(string key, byte[] bytes);

        /// <summary>Persists any object that <see cref="DBreezeSerializer"/> can serialize to the database.</summary>
        void SaveValue<T>(string key, T value);

        /// <summary>Persists any object to the database. Object is stored as JSON.</summary>
        void SaveValueJson<T>(string key, T value);

        /// <summary>Loads byte array from the database.</summary>
        byte[] LoadBytes(string key);

        /// <summary>Loads an object that <see cref="DBreezeSerializer"/> can deserialize from the database.</summary>
        T LoadValue<T>(string key);

        /// <summary>Loads JSON from the database and deserializes it.</summary>
        T LoadValueJson<T>(string key);
    }

    public class KeyValueRepository : IKeyValueRepository
    {
        /// <summary>Access to DBreeze database.</summary>
        private readonly IKeyValueStore keyValueStore;

        private const string TableName = "common";

        private readonly DBreezeSerializer dBreezeSerializer;

        public KeyValueRepository(IKeyValueRepositoryStore keyValueRepositoryStore, DBreezeSerializer dBreezeSerializer)
        {
            this.keyValueStore = keyValueRepositoryStore;
            this.dBreezeSerializer = dBreezeSerializer;
        }

        /// <inheritdoc />
        public void SaveBytes(string key, byte[] bytes)
        {
            byte[] keyBytes = Encoding.ASCII.GetBytes(key);

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                transaction.Insert<byte[], byte[]>(TableName, keyBytes, bytes);

                transaction.Commit();
            }
        }

        /// <inheritdoc />
        public void SaveValue<T>(string key, T value)
        {
            this.SaveBytes(key, this.dBreezeSerializer.Serialize(value));
        }

        /// <inheritdoc />
        public void SaveValueJson<T>(string key, T value)
        {
            string json = Serializer.ToString(value);
            byte[] jsonBytes = Encoding.ASCII.GetBytes(json);

            this.SaveBytes(key, jsonBytes);
        }

        /// <inheritdoc />
        public byte[] LoadBytes(string key)
        {
            byte[] keyBytes = Encoding.ASCII.GetBytes(key);

            using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                if (!transaction.Select(TableName, keyBytes, out byte[] value))
                    return null;

                return value;
            }
        }

        /// <inheritdoc />
        public T LoadValue<T>(string key)
        {
            byte[] bytes = this.LoadBytes(key);

            if (bytes == null)
                return default(T);

            T value = this.dBreezeSerializer.Deserialize<T>(bytes);
            return value;
        }

        /// <inheritdoc />
        public T LoadValueJson<T>(string key)
        {
            byte[] bytes = this.LoadBytes(key);

            if (bytes == null)
                return default(T);

            string json = Encoding.ASCII.GetString(bytes);

            T value = Serializer.ToObject<T>(json);

            return value;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.keyValueStore.Dispose();
        }
    }
}

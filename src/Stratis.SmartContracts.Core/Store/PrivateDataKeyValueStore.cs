﻿using System;
using System.Linq;
using DBreeze.Utils;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStoreLevelDB;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Core.Store
{
    public interface IPrivateDataKeyValueStore : IKeyValueRepositoryStore
    {
    }

    public interface IPrivateDataStore
    {
        void StoreBytes(uint160 contractAddress, byte[] key, byte[] value);
        byte[] GetBytes(uint160 contractAddress, byte[] key);
    }

    public class PrivateDataStore : IPrivateDataStore
    {
        public const string Table = "private";
        private readonly IPrivateDataKeyValueStore store;

        public PrivateDataStore(IPrivateDataKeyValueStore privateDataKeyValueStore)
        {
            this.store = privateDataKeyValueStore;
        }

        public void StoreBytes(uint160 contractAddress, byte[] key, byte[] value)
        {
            using (IKeyValueStoreTransaction tx = this.store.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
            {
                var compositeKey = PrivateDataStoreQueryParams.CreateCompositeKeyForContract(contractAddress, key);

                tx.Insert(Table, compositeKey, value);

                tx.Commit();
            }
        }

        public byte[] GetBytes(uint160 contractAddress, byte[] key)
        {
            using (IKeyValueStoreTransaction tx = this.store.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                var compositeKey = PrivateDataStoreQueryParams.CreateCompositeKeyForContract(contractAddress, key);

                if (tx.Select(Table, compositeKey, out byte[] result))
                {
                    return result;
                }

                return null;
            }
        }
    }

    public static class PrivateDataStoreQueryParams
    {
        public static byte[] CreateCompositeKeyForContract(uint160 contractAddress, byte[] key)
        {
            // Don't think we care about endianness here as lexographical ordering isn't important for this key.
            var compositeKey = new byte[0];
            compositeKey = compositeKey.Combine(contractAddress.ToBytes());
            compositeKey = compositeKey.Combine(TransientStoreQueryParams.CompositeKeySeparator);
            compositeKey = compositeKey.Combine(key);

            return compositeKey;
        }

        public static (uint160 contractAddress, byte[] key) SplitCompositeKeyForContract(byte[] key)
        {
            const int uint160WidthBytes = 20;

            var txIdBytes = key.Take(uint160WidthBytes).ToArray();
            var keyBytes = key.Skip(uint160WidthBytes + TransientStoreQueryParams.CompositeKeySeparator.Length).ToArray();

            return (new uint160(txIdBytes), keyBytes);
        }
    }

    public class PrivateDataKeyValueStore : KeyValueStoreLevelDB, IPrivateDataKeyValueStore
    {
        public PrivateDataKeyValueStore(DataFolder dataFolder, ILoggerFactory loggerFactory, IRepositorySerializer repositorySerializer) 
            : base(dataFolder.PrivateDataStorePath, loggerFactory, repositorySerializer)
        {
        }
    }
}
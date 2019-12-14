using System;
using System.IO;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.NodeStorage
{
    public class Migrate
    {
        public void MigrateKeyValueStore<TFrom, TTo>(Network network, DataFolder sourceDataFolder, DataFolder targetDataFolder) where TFrom : KeyValueStoreRepository where TTo : KeyValueStoreRepository
        {
            // Copy Block Store.
            using (var blockStoreSource = new KeyValueStore<TFrom>(sourceDataFolder.BlockPath, new LoggerFactory(), DateTimeProvider.Default, new RepositorySerializer(network.Consensus.ConsensusFactory)))
            {
                using (var blockStoreTarget = new KeyValueStore<TTo>(targetDataFolder.BlockPath, new LoggerFactory(), DateTimeProvider.Default, new RepositorySerializer(network.Consensus.ConsensusFactory)))
                {
                    CopyTable<byte[], byte[]>(blockStoreSource, blockStoreTarget, "Block");
                    CopyTable<byte[], byte[]>(blockStoreSource, blockStoreTarget, "Transaction");
                    CopyTable<byte[], byte[]>(blockStoreSource, blockStoreTarget, "Common", (tableName, from, to) =>
                    {
                        byte[] txIndexKey = new byte[1];

                        if (from.Select(tableName, txIndexKey, out bool txIndex))
                            to.Insert(tableName, txIndexKey, txIndex);
                    });
                }
            }

            // Copy Chain Repository.
            using (var chainRepoSource = new KeyValueStore<TFrom>(sourceDataFolder.ChainPath, new LoggerFactory(), DateTimeProvider.Default, new RepositorySerializer(network.Consensus.ConsensusFactory)))
            {
                using (var chainRepoTarget = new KeyValueStore<TTo>(targetDataFolder.ChainPath, new LoggerFactory(), DateTimeProvider.Default, new RepositorySerializer(network.Consensus.ConsensusFactory)))
                {
                    // Primitive types must be used.
                    CopyTable<int, byte[]>(chainRepoSource, chainRepoTarget, "Chain");
                }
            }

            // Copy CoinView.
            using (var coinViewSource = new KeyValueStore<TFrom>(sourceDataFolder.CoinViewPath, new LoggerFactory(), DateTimeProvider.Default, new RepositorySerializer(network.Consensus.ConsensusFactory)))
            {
                using (var coinViewTarget = new KeyValueStore<TTo>(targetDataFolder.CoinViewPath, new LoggerFactory(), DateTimeProvider.Default, new RepositorySerializer(network.Consensus.ConsensusFactory)))
                {
                    CopyTable<byte[], byte[]>(coinViewSource, coinViewTarget, "Coins");
                    // Primitive types must be used.
                    CopyTable<int, byte[]>(coinViewSource, coinViewTarget, "Rewind");
                    CopyTable<byte[], byte[]>(coinViewSource, coinViewTarget, "Stake");
                    CopyTable<byte[], byte[]>(coinViewSource, coinViewTarget, "BlockHash");
                }
            }

            // Copy ProvenBlockHeader.
            using (var provenSource = new KeyValueStore<TFrom>(sourceDataFolder.ProvenBlockHeaderPath, new LoggerFactory(), DateTimeProvider.Default, new RepositorySerializer(network.Consensus.ConsensusFactory)))
            {
                using (var provenTarget = new KeyValueStore<TTo>(targetDataFolder.ProvenBlockHeaderPath, new LoggerFactory(), DateTimeProvider.Default, new RepositorySerializer(network.Consensus.ConsensusFactory)))
                {
                    // Primitive types must be used.
                    CopyTable<int, byte[]>(provenSource, provenTarget, "ProvenBlockHeader");
                    CopyTable<byte[], byte[]>(provenSource, provenTarget, "BlockHashHeight");
                }
            }

            if (Directory.Exists(Path.Combine(sourceDataFolder.RootPath, "finalizedBlock")))
            {
                string targetFolder = Path.Combine(targetDataFolder.RootPath, "finalizedBlock");
                if (Directory.Exists(targetFolder))
                    Directory.Delete(targetFolder, true);
                Directory.CreateDirectory(targetFolder);
                foreach (string file in Directory.EnumerateFiles(Path.Combine(sourceDataFolder.RootPath, "finalizedBlock"), "*.*", SearchOption.TopDirectoryOnly))
                    File.Copy(file, file.Replace("ReadyData", "ReadyDataLevelDB"));
            }

            foreach (string file in Directory.EnumerateFiles(sourceDataFolder.RootPath, "*.*", SearchOption.TopDirectoryOnly))
            {
                string targetName = file.Replace("ReadyData", "ReadyDataLevelDB");
                if (File.Exists(targetName))
                    File.Delete(targetName);
                File.Copy(file, targetName);
            }
        }

        private static void CopyTable<K, V>(IKeyValueStore keyValueStoreFrom, IKeyValueStore keyValueStoreTo, string tableName,
            Action<string, IKeyValueStoreTransaction, IKeyValueStoreTransaction> action = null)
        {
            using (IKeyValueStoreTransaction tranFrom = keyValueStoreFrom.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                using (IKeyValueStoreTransaction tranTo = keyValueStoreTo.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
                {
                    foreach ((K key, V value) in tranFrom.SelectForward<K, V>(tableName))
                    {
                        tranTo.Insert(tableName, key, value);
                    }

                    // Copy primitives explicitly using the correct types.
                    action?.Invoke(tableName, tranFrom, tranTo);

                    tranTo.Commit();
                }
            }
        }
    }
}

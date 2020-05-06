﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;
using Stratis.Core.AsyncWork;

namespace Stratis.Bitcoin.NodeStorage
{
    public class Migrate
    {
        public void MigrateKeyValueStore<TFrom, TTo>(Network network, DataFolder sourceDataFolder, DataFolder targetDataFolder) where TFrom : IKeyValueStoreRepository where TTo : IKeyValueStoreRepository
        {
            void CopyTable<K, V>(IKeyValueStore keyValueStoreFrom, IKeyValueStore keyValueStoreTo, string tableName, Action<string, IKeyValueStoreTransaction, IKeyValueStoreTransaction> action = null)
            {
                using (IKeyValueStoreTransaction tranFrom = keyValueStoreFrom.CreateTransaction(KeyValueStoreTransactionMode.Read))
                {
                    using (IKeyValueStoreTransaction tranTo = keyValueStoreTo.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite))
                    {
                        if (tableName == "Block" && typeof(TFrom) == typeof(KeyValueStoreDBreeze.KeyValueStoreDBreeze))
                        {
                            var blocks = tranFrom.SelectDictionary<byte[], byte[]>("Block");
                            var prevBlock = blocks.ToDictionary(kv => kv.Key, kv => kv.Value.Skip(4).Take(32).ToArray(), new ByteArrayComparer());

                            // Determine block heights from prevBlock values.
                            var blockHeight = new Dictionary<byte[], int>(new ByteArrayComparer());
                            blockHeight[network.GenesisHash.ToBytes()] = 0;
                            foreach (KeyValuePair<byte[], byte[]> kv in blocks)
                            {
                                if (blockHeight.TryGetValue(kv.Key, out int height))
                                    continue;

                                var stack = new Stack<byte[]>();
                                var key = kv.Key;
                                do
                                {
                                    if (!prevBlock.TryGetValue(key, out byte[] newKey))
                                    {
                                        blockHeight[key] = 1;
                                        break;
                                    }

                                    stack.Push(key);
                                    key = newKey;
                                } while (!blockHeight.ContainsKey(key));

                                height = blockHeight[key];
                                while (stack.Count > 0)
                                {
                                    key = stack.Pop();
                                    blockHeight[key] = ++height;
                                }
                            }

                            // Insert height:hash and value to target.
                            foreach ((byte[] key, V value) in tranFrom.SelectAll<byte[], V>(tableName))
                            {
                                tranTo.Insert(tableName, BitConverter.GetBytes(blockHeight[key]).Reverse().Concat(key).ToArray(), value);
                            }
                        }
                        else
                        {
                            foreach ((K key, V value) in tranFrom.SelectAll<K, V>(tableName))
                            {
                                tranTo.Insert(tableName, key, value);
                            }
                        }

                        // Copy primitives explicitly using the correct types.
                        action?.Invoke(tableName, tranFrom, tranTo);

                        tranTo.Commit();
                    }
                }
            }

            // Copy Block Store.
            if (Directory.Exists(sourceDataFolder.BlockPath))
            {
                using (var blockStoreSource = new KeyValueStoreLevelDB.KeyValueStoreLevelDB(sourceDataFolder.BlockPath, new LoggerFactory(), new RepositorySerializer(network.Consensus.ConsensusFactory)))
                {
                    using (var blockStoreTarget = new KeyValueStoreLevelDB.KeyValueStoreLevelDB(targetDataFolder.BlockPath, new LoggerFactory(), new RepositorySerializer(network.Consensus.ConsensusFactory)))
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
            }

            // Copy Chain Repository.
            if (Directory.Exists(sourceDataFolder.ChainPath))
            {
                using (var chainRepoSource = new KeyValueStoreLevelDB.KeyValueStoreLevelDB(sourceDataFolder.ChainPath, new LoggerFactory(), new RepositorySerializer(network.Consensus.ConsensusFactory)))
                {
                    using (var chainRepoTarget = new KeyValueStoreLevelDB.KeyValueStoreLevelDB(targetDataFolder.ChainPath, new LoggerFactory(), new RepositorySerializer(network.Consensus.ConsensusFactory)))
                    {
                        // Primitive types must be used.
                        CopyTable<int, byte[]>(chainRepoSource, chainRepoTarget, "Chain");
                    }
                }
            }

            // Copy CoinView.
            if (Directory.Exists(sourceDataFolder.CoinViewPath))
            {
                using (var coinViewSource = new KeyValueStoreLevelDB.KeyValueStoreLevelDB(sourceDataFolder.CoinViewPath, new LoggerFactory(), new RepositorySerializer(network.Consensus.ConsensusFactory)))
                {
                    using (var coinViewTarget = new KeyValueStoreLevelDB.KeyValueStoreLevelDB(targetDataFolder.CoinViewPath, new LoggerFactory(), new RepositorySerializer(network.Consensus.ConsensusFactory)))
                    {
                        CopyTable<byte[], byte[]>(coinViewSource, coinViewTarget, "Coins");
                        // Primitive types must be used.
                        CopyTable<int, byte[]>(coinViewSource, coinViewTarget, "Rewind");
                        CopyTable<byte[], byte[]>(coinViewSource, coinViewTarget, "Stake");
                        CopyTable<byte[], byte[]>(coinViewSource, coinViewTarget, "BlockHash");
                    }
                }
            }

            // Copy ProvenBlockHeader.
            if (Directory.Exists(sourceDataFolder.ProvenBlockHeaderPath))
            {
                using (var provenSource = new KeyValueStoreLevelDB.KeyValueStoreLevelDB(sourceDataFolder.ProvenBlockHeaderPath, new LoggerFactory(), new RepositorySerializer(network.Consensus.ConsensusFactory)))
                {
                    using (var provenTarget = new KeyValueStoreLevelDB.KeyValueStoreLevelDB(targetDataFolder.ProvenBlockHeaderPath, new LoggerFactory(), new RepositorySerializer(network.Consensus.ConsensusFactory)))
                    {
                        // Primitive types must be used.
                        CopyTable<int, byte[]>(provenSource, provenTarget, "ProvenBlockHeader");
                        CopyTable<byte[], byte[]>(provenSource, provenTarget, "BlockHashHeight");
                    }
                }
            }

            // Copy KeyValueRepository.
            if (Directory.Exists(sourceDataFolder.KeyValueRepositoryPath))
            {
                using (var kvSource = new KeyValueStoreLevelDB.KeyValueStoreLevelDB(sourceDataFolder.KeyValueRepositoryPath, new LoggerFactory(), new RepositorySerializer(network.Consensus.ConsensusFactory)))
                {
                    using (var kvTarget = new KeyValueStoreLevelDB.KeyValueStoreLevelDB(targetDataFolder.KeyValueRepositoryPath, new LoggerFactory(), new RepositorySerializer(network.Consensus.ConsensusFactory)))
                    {
                        // Primitive types must be used.
                        CopyTable<byte[], byte[]>(kvSource, kvTarget, "common");
                    }
                }
            }

            // Copy SmartContractState.
            if (Directory.Exists(sourceDataFolder.SmartContractStatePath))
            {
                using (var kvSource = new KeyValueStoreLevelDB.KeyValueStoreLevelDB(sourceDataFolder.SmartContractStatePath, new LoggerFactory(), new RepositorySerializer(network.Consensus.ConsensusFactory)))
                {
                    using (var kvTarget = new KeyValueStoreLevelDB.KeyValueStoreLevelDB(targetDataFolder.SmartContractStatePath, new LoggerFactory(), new RepositorySerializer(network.Consensus.ConsensusFactory)))
                    {
                        foreach (string tableName in kvSource.GetTables())
                        {
                            CopyTable<byte[], byte[]>(kvSource, kvTarget, tableName);
                        }
                    }
                }
            }

            // Copy the files in the data folder root.
            foreach (string file in Directory.EnumerateFiles(sourceDataFolder.RootPath, "*.*", SearchOption.TopDirectoryOnly))
            {
                string targetName = Path.Combine(targetDataFolder.RootPath, Path.GetFileName(file));
                if (File.Exists(targetName))
                    File.Delete(targetName);
                File.Copy(file, targetName);
            }
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DBreeze;
using DBreeze.DataTypes;
using FluentAssertions;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Core.AsyncWork;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    /// <summary>
    /// Tests of DBreeze database and <see cref="RepositorySerializer"/> class.
    /// </summary>
    public class DBreezeTest : TestBase
    {
        /// <summary>Provider of binary (de)serialization for data stored in the database.</summary>
        private readonly RepositorySerializer repositorySerializer;

        /// <summary>
        /// Initializes the DBreeze serializer.
        /// </summary>
        public DBreezeTest() : base(KnownNetworks.StratisRegTest)
        {
            this.repositorySerializer = new RepositorySerializer(this.Network.Consensus.ConsensusFactory);
        }

        [Fact]
        public void SerializerWithBitcoinSerializableReturnsAsBytes()
        {
            Block block = KnownNetworks.StratisRegTest.Consensus.ConsensusFactory.CreateBlock();

            byte[] result = this.repositorySerializer.Serialize(block);

            Assert.Equal(block.ToBytes(), result);
        }

        [Fact]
        public void SerializerWithUint256ReturnsAsBytes()
        {
            var val = new uint256();

            byte[] result = this.repositorySerializer.Serialize(val);

            Assert.Equal(val.ToBytes(), result);
        }

        [Fact]
        public void SerializerWithUnsupportedObjectThrowsException()
        {
            Assert.Throws<NotSupportedException>(() =>
            {
                var shouldThrowException = new NotSupportedClass();

                this.repositorySerializer.Serialize(shouldThrowException);
            });
        }

        [Fact]
        public void DeserializerWithCoinsDeserializesObject()
        {
            Network network = KnownNetworks.StratisRegTest;
            Block genesis = network.GetGenesis();
            var coins = new Coins(genesis.Transactions[0], 0);

            var result = this.repositorySerializer.Deserialize<Coins>(coins.ToBytes(KnownNetworks.StratisRegTest.Consensus.ConsensusFactory));

            Assert.Equal(coins.CoinBase, result.CoinBase);
            Assert.Equal(coins.Height, result.Height);
            Assert.Equal(coins.IsEmpty, result.IsEmpty);
            Assert.Equal(coins.IsPruned, result.IsPruned);
            Assert.Equal(coins.Outputs.Count, result.Outputs.Count);
            Assert.Equal(coins.Outputs[0].ScriptPubKey.Hash, result.Outputs[0].ScriptPubKey.Hash);
            Assert.Equal(coins.Outputs[0].Value, result.Outputs[0].Value);
            Assert.Equal(coins.UnspentCount, result.UnspentCount);
            Assert.Equal(coins.Value, result.Value);
            Assert.Equal(coins.Version, result.Version);
        }

        [Fact]
        public void DeserializerWithBlockHeaderDeserializesObject()
        {
            Network network = KnownNetworks.StratisRegTest;
            Block genesis = network.GetGenesis();
            BlockHeader blockHeader = genesis.Header;

            var result = this.repositorySerializer.Deserialize<BlockHeader>(blockHeader.ToBytes(KnownNetworks.StratisRegTest.Consensus.ConsensusFactory));

            Assert.Equal(blockHeader.GetHash(), result.GetHash());
        }

        [Fact]
        public void DeserializerWithRewindDataDeserializesObject()
        {
            Network network = KnownNetworks.StratisRegTest;
            Block genesis = network.GetGenesis();
            var rewindData = new RewindData(genesis.GetHash());

            var result = this.repositorySerializer.Deserialize<RewindData>(rewindData.ToBytes());

            Assert.Equal(genesis.GetHash(), result.PreviousBlockHash);
        }

        [Fact]
        public void DeserializerWithuint256DeserializesObject()
        {
            uint256 val = uint256.One;

            var result = this.repositorySerializer.Deserialize<uint256>(val.ToBytes());

            Assert.Equal(val, result);
        }

        [Fact]
        public void DeserializerWithBlockDeserializesObject()
        {
            Network network = KnownNetworks.StratisRegTest;
            Block block = network.GetGenesis();

            var result = this.repositorySerializer.Deserialize<Block>(block.ToBytes(KnownNetworks.StratisRegTest.Consensus.ConsensusFactory));

            Assert.Equal(block.GetHash(), result.GetHash());
        }

        class NotSupportedClass
        {

        };

        [Fact]
        public void DeserializerWithNotSupportedClassThrowsException()
        {
            Assert.Throws<NotSupportedException>(() =>
            {
                string test = "Should throw exception.";

                this.repositorySerializer.Deserialize<NotSupportedClass>(Encoding.UTF8.GetBytes(test));
            });
        }

        private class UnknownBitcoinSerialisable : IBitcoinSerializable
        {
            public int ReadWriteCalls;

            public void ReadWrite(BitcoinStream stream) { this.ReadWriteCalls++; }
        }

        [Fact]
        public void DeserializeAnyIBitcoinSerializableDoesNotThrowException()
        {
            var result = this.repositorySerializer.Deserialize<UnknownBitcoinSerialisable>(Encoding.UTF8.GetBytes("useless"));
            result.ReadWriteCalls.Should().Be(1);
        }

        [Fact]
        public void SerializeAnyIBitcoinSerializableDoesNotThrowException()
        {
            var serialisable = new UnknownBitcoinSerialisable();
            this.repositorySerializer.Serialize(serialisable);
            serialisable.ReadWriteCalls.Should().Be(1);
        }

        [Fact]
        public void DBreezeEngineAbleToAccessExistingTransactionData()
        {
            string dir = CreateTestDir(this);
            uint256[] data = SetupTransactionData(dir);

            using (var engine = new DBreezeEngine(dir))
            {
                using (DBreeze.Transactions.Transaction transaction = engine.GetTransaction())
                {
                    var data2 = new uint256[data.Length];
                    int i = 0;
                    foreach (Row<int, byte[]> row in transaction.SelectForward<int, byte[]>("Table"))
                    {
                        data2[i++] = new uint256(row.Value, false);
                    }

                    Assert.True(data.SequenceEqual(data2));
                }
            }
        }

        private static uint256[] SetupTransactionData(string folder)
        {
            using (var engine = new DBreezeEngine(folder))
            {
                var data = new[]
                {
                    new uint256(3),
                    new uint256(2),
                    new uint256(5),
                    new uint256(10),
                };

                int i = 0;
                using (DBreeze.Transactions.Transaction tx = engine.GetTransaction())
                {
                    foreach (uint256 d in data)
                        tx.Insert<int, byte[]>("Table", i++, d.ToBytes(false));

                    tx.Commit();
                }

                return data;
            }
        }

        [Fact]
        public void IsAbleToSerializeCollections()
        {
            var data = new List<uint256>
            {
                new uint256(3),
                new uint256(2),
                new uint256(5),
                new uint256(10),
            };

            byte[] bytes1 = this.repositorySerializer.Serialize(data);
            byte[] bytes2 = this.repositorySerializer.Serialize(data.ToArray());
            Assert.True(bytes1.SequenceEqual(bytes2));

            this.repositorySerializer.Serialize(data.ToHashSet());
        }
    }
}
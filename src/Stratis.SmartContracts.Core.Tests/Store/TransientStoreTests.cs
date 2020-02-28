using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.Store;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests.Store
{
    public class TransientStoreTests
    {
        private TransientStore store;
        private Mock<ITransientKeyValueStore> repository;

        public TransientStoreTests()
        {
            this.repository = new Mock<ITransientKeyValueStore>();
            this.store = new TransientStore(this.repository.Object);
        }

        [Fact]
        public void TransientStoreKey_Uses_TxId_Copy()
        {
            var txId = new byte[] {0x00};
            
            var key = new TransientStoreKey(txId, Guid.Empty, 0);

            Assert.NotSame(txId, key.TxId);
        }

        [Fact]
        public void TransientStoreKey_Serializes_Correctly()
        {
            var txId = new byte[] { 0xAA, 0xBB, 0xCC };
            var guid = Guid.NewGuid();
            var blockHeight = uint.MaxValue;

            var key = new TransientStoreKey(txId, guid, blockHeight);

            var keyBytes = key.ToBytes();

            Assert.True(keyBytes.Take(txId.Length).ToArray().SequenceEqual(txId));
            Assert.True(keyBytes.Skip(txId.Length).Take(guid.ToByteArray().Length).ToArray().SequenceEqual(guid.ToByteArray()));
            var blockHeightBytes = BitConverter.GetBytes(blockHeight);
            Assert.True(keyBytes.Skip(txId.Length + guid.ToByteArray().Length).Take(blockHeightBytes.Length).ToArray().SequenceEqual(blockHeightBytes));
        }

        [Fact]
        public void Can_Persist_Data()
        {
            var transaction = new Mock<IKeyValueStoreTransaction>();

            this.repository.Setup(r => r.CreateTransaction(
                It.IsAny<KeyValueStoreTransactionMode>(),
                It.IsAny<string>()
            ))
            .Returns(transaction.Object);

            var txId = uint256.One;
            uint blockHeight = 1;

            var rws = new ReadWriteSet();
            rws.AddReadItem(new ReadWriteSetKey(uint160.One, new byte[] { 0xAA}), "1");
            rws.AddWriteItem(new ReadWriteSetKey(uint160.One, new byte[] { 0xCC }), new byte[]{ 0xDD });

            var writeSet = new ReadWriteSet();
            writeSet.MergeWriteSet(rws);
            var serializedWriteSet = writeSet.ToJsonString();

            this.store.Persist(txId, blockHeight, rws);

            transaction.Verify(t => t.Insert(TransientStore.Table, It.IsAny<byte[]>(), serializedWriteSet));
            transaction.Verify(t => t.Commit(), Times.Once);
        }
    }
}

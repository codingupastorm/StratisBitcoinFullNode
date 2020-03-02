using System;
using System.Linq;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
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

            uint256 txId = uint256.One;
            uint blockHeight = 1;
            var privateData = new byte[] {0xAA, 0xBB, 0xCC};
            var data = new TransientStorePrivateData(privateData);

            this.store.Persist(txId, blockHeight, data);

            transaction.Verify(t => t.Insert(
                TransientStore.Table, 
                It.IsAny<byte[]>(), 
                It.Is<byte[]>(d => privateData.SequenceEqual(data.ToBytes()))
            ));

            var purgeKey = new CompositePurgeIndexKey(blockHeight);

            // Verify the purge key was inserted.
            transaction.Verify(t => t.Insert(
                TransientStore.Table,
                It.Is<byte[]>(d => purgeKey.ToBytes().SequenceEqual(d)),
                It.IsAny<byte[]>()
            ));

            transaction.Verify(t => t.Commit(), Times.Once);
        }
    }
}

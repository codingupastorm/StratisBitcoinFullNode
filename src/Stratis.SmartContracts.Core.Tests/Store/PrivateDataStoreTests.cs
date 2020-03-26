using System.Linq;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.SmartContracts.Core.Store;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests.Store
{
    public class PrivateDataStoreTests
    {
        private PrivateDataStore store;
        private Mock<IPrivateDataKeyValueStore> keyValueStore;

        public PrivateDataStoreTests()
        {
            this.keyValueStore = new Mock<IPrivateDataKeyValueStore>();
            this.store = new PrivateDataStore(this.keyValueStore.Object);
        }

        [Fact]
        public void Can_Persist_Data()
        {
            var transaction = new Mock<IKeyValueStoreTransaction>();

            this.keyValueStore.Setup(r => r.CreateTransaction(
                    It.IsAny<KeyValueStoreTransactionMode>()
                ))
                .Returns(transaction.Object);

            var contractAddress = new uint160(RandomUtils.GetBytes(20));
            var key = RandomUtils.GetBytes(64);
            var value = RandomUtils.GetBytes(256);

            this.store.StoreBytes(contractAddress, key, value);
            
            transaction.Verify(t => t.Insert(
                PrivateDataStore.Table,
                It.Is<byte[]>(d => d.SequenceEqual(PrivateDataStoreQueryParams.CreateCompositeKeyForContract(contractAddress, key))),
                It.Is<byte[]>(d => d.SequenceEqual(value))
            ));

            transaction.Verify(t => t.Commit(), Times.Once);
        }

        [Fact]
        public void Can_Get_Data()
        {
            var transaction = new Mock<IKeyValueStoreTransaction>();

            this.keyValueStore.Setup(r => r.CreateTransaction(
                    It.IsAny<KeyValueStoreTransactionMode>()
                ))
                .Returns(transaction.Object);

            var contractAddress = new uint160(RandomUtils.GetBytes(20));
            var key = RandomUtils.GetBytes(64);
            var value = RandomUtils.GetBytes(256);

            transaction.Setup(t => t.Select(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    out value
                ))
                .Returns(true);
            
            byte[] result = this.store.GetBytes(contractAddress, key);

            Assert.True(value.SequenceEqual(result));

            object _;
            transaction.Verify(t => t.Select(
                PrivateDataStore.Table,
                It.Is<byte[]>(d => d.SequenceEqual(PrivateDataStoreQueryParams.CreateCompositeKeyForContract(contractAddress, key))),
                out _
                ));

            transaction.Verify(t => t.Commit(), Times.Never);
        }
    }
}
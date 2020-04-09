using System;
using System.Linq;
using Moq;
using NBitcoin;
using Stratis.SmartContracts.CLR.Metering;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Store;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class PrivateMeteredPersistenceStrategyTests
    {
        private readonly IKeyEncodingStrategy keyEncodingStrategy = BasicKeyEncodingStrategy.Default;

        [Fact]
        public void Fetches_Bytes_From_Private_State_If_Not_In_RWS()
        {
            byte[] testKey = new byte[] { 1 };
            byte[] testValue = new byte[] { 2 };
            uint160 testAddress = uint160.One;
            var testVersion = "1.1";
            var testStorageValue = new StorageValue(testValue, testVersion);
            var testRwsKey = new ReadWriteSetKey(testAddress, testKey);

            var sr = new Mock<IPrivateDataStore>();
            var rws = new Mock<IReadWriteSetOperations>();

            byte[] value = null;

            rws.Setup(r => r.GetWriteItem(testRwsKey, out value)).Returns(false);

            sr.Setup(m => m.GetBytes(
                It.IsAny<uint160>(),
                It.IsAny<byte[]>()))
                .Returns(testStorageValue.ToBytes());

            var availableGas = (RuntimeObserver.Gas)100000;
            GasMeter gasMeter = new GasMeter(availableGas);

            var strategy = new PrivateMeteredPersistenceStrategy(
                sr.Object,
                gasMeter,
                this.keyEncodingStrategy,
                rws.Object,
                testVersion
            );

            var result = strategy.FetchBytes(
                testAddress,
                testKey);

            rws.Verify(r => r.GetWriteItem(testRwsKey, out value), Times.Once);
            sr.Verify(s => s.GetBytes(testAddress, testKey), Times.Once);
            Assert.True(testValue.SequenceEqual(result));
        }

        [Fact]
        public void Fetches_Bytes_From_RWS()
        {
            byte[] testKey = new byte[] { 1 };
            byte[] testValue = new byte[] { 2 };
            uint160 testAddress = uint160.One;
            var testVersion = "1.1";
            var testStorageValue = new StorageValue(testValue, testVersion);
            var testRwsKey = new ReadWriteSetKey(testAddress, testKey);

            var sr = new Mock<IPrivateDataStore>();
            var rws = new Mock<IReadWriteSetOperations>();

            rws.Setup(r => r.GetWriteItem(testRwsKey, out testValue)).Returns(true);

            var availableGas = (RuntimeObserver.Gas)100000;
            GasMeter gasMeter = new GasMeter(availableGas);

            var strategy = new PrivateMeteredPersistenceStrategy(
                sr.Object,
                gasMeter,
                this.keyEncodingStrategy,
                rws.Object,
                testVersion
            );

            var result = strategy.FetchBytes(
                testAddress,
                testKey);

            // Should hit the RWS
            rws.Verify(r => r.GetWriteItem(testRwsKey, out testValue), Times.Once);

            // Should never hit the storage.
            sr.Verify(s => s.GetBytes(testAddress, testKey), Times.Never);

            Assert.True(testValue.SequenceEqual(result));
        }

        [Fact]
        public void Sets_Bytes_In_RWS()
        {
            byte[] testKey = new byte[] { 1 };
            byte[] testValue = new byte[] { 2 };
            uint160 testAddress = uint160.One;
            var testVersion = "1.1";
            var testStorageValue = new StorageValue(testValue, testVersion);

            var sr = new Mock<IPrivateDataStore>();
            var rws = Mock.Of<IReadWriteSetOperations>();

            sr.Setup(m => m.StoreBytes(
                It.IsAny<uint160>(),
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>()));

            var availableGas = (RuntimeObserver.Gas)100000;
            GasMeter gasMeter = new GasMeter(availableGas);

            var strategy = new PrivateMeteredPersistenceStrategy(
                sr.Object,
                gasMeter,
                this.keyEncodingStrategy,
                rws,
                testVersion
            );

            strategy.StoreBytes(
                testAddress,
                testKey,
                testValue);

            sr.Verify(s => s.StoreBytes(testAddress, testKey, It.Is<byte[]>(b => testStorageValue.ToBytes().SequenceEqual(b))));
        }

        [Fact]
        public void GasConsumed_Success()
        {
            byte[] testKey = new byte[] { 1 };
            byte[] testValue = new byte[] { 2 };
            uint160 testAddress = uint160.One;

            var sr = new Mock<IPrivateDataStore>();
            var rws = Mock.Of<IReadWriteSetOperations>();

            sr.Setup(m => m.StoreBytes(
                It.IsAny<uint160>(),
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>()));

            var availableGas = (RuntimeObserver.Gas)100000;
            GasMeter gasMeter = new GasMeter(availableGas);

            var strategy = new PrivateMeteredPersistenceStrategy(
                sr.Object,
                gasMeter,
                this.keyEncodingStrategy,
                rws,
                "1.1"
            );

            strategy.StoreBytes(
                testAddress,
                testKey,
                testValue);

            // Test that gas is used
            Assert.True(gasMeter.GasConsumed < availableGas);
        }
    }
}
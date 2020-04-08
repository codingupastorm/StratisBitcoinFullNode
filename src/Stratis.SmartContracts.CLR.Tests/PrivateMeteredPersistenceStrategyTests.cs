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
        public void Fetches_Bytes_From_Private_State()
        {
            byte[] testKey = new byte[] { 1 };
            byte[] testValue = new byte[] { 2 };
            uint160 testAddress = uint160.One;

            var sr = new Mock<IPrivateDataStore>();
            var rws = Mock.Of<IReadWriteSetOperations>();

            sr.Setup(m => m.GetBytes(
                It.IsAny<uint160>(),
                It.IsAny<byte[]>()))
                .Returns(testValue);

            var availableGas = (RuntimeObserver.Gas)100000;
            GasMeter gasMeter = new GasMeter(availableGas);

            var strategy = new PrivateMeteredPersistenceStrategy(
                sr.Object,
                gasMeter,
                this.keyEncodingStrategy,
                rws,
                "1.1"
            );

            var result = strategy.FetchBytes(
                testAddress,
                testKey);

            sr.Verify(s => s.GetBytes(testAddress, testKey), Times.Once);
            Assert.True(testValue.SequenceEqual(result));
        }

        [Fact]
        public void Sets_Bytes_In_Private_State()
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

            sr.Verify(s => s.StoreBytes(testAddress, testKey, testValue));
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

            sr.Verify(s => s.StoreBytes(testAddress, testKey, testValue));

            // Test that gas is used
            Assert.True(gasMeter.GasConsumed < availableGas);
        }
    }
}
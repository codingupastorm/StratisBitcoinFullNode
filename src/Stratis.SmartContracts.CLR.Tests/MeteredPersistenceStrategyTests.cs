using System;
using Moq;
using NBitcoin;
using Stratis.SmartContracts.CLR.Metering;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;
using Xunit;

namespace Stratis.SmartContracts.CLR.Tests
{
    public class MeteredPersistenceStrategyTests
    {
        private readonly IKeyEncodingStrategy keyEncodingStrategy = BasicKeyEncodingStrategy.Default;

        [Fact]
        public void SmartContracts_MeteredPersistenceStrategy_TestNullInjectedArgsThrow()
        {
            var sr = new Mock<IStateRepository>();

            Assert.Throws<ArgumentNullException>(() => new MeteredPersistenceStrategy(null, new GasMeter((RuntimeObserver.Gas) 0), this.keyEncodingStrategy, new ReadWriteSet(),  "1.1"));
            Assert.Throws<ArgumentNullException>(() => new MeteredPersistenceStrategy(sr.Object, null, this.keyEncodingStrategy, new ReadWriteSet(),  "1.1"));
        }

        [Fact]
        public void SmartContracts_MeteredPersistenceStrategy_TestGasConsumed()
        {
            byte[] testKey = new byte[] { 1 };
            byte[] testValue = new byte[] { 2 };
            uint160 testAddress = uint160.One;
            const string testVersion = "1.1";

            var sr = new Mock<IStateRepository>();

            sr.Setup(m => m.SetStorageValue(
                It.IsAny<uint160>(),
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>(), "1.1"));

            var availableGas = (RuntimeObserver.Gas) 100000;
            GasMeter gasMeter = new GasMeter(availableGas);

            MeteredPersistenceStrategy strategy = new MeteredPersistenceStrategy(
                sr.Object,
                gasMeter,
                this.keyEncodingStrategy,
                new ReadWriteSet(), 
                "1.1"
                );

            strategy.StoreBytes(
                testAddress, 
                testKey,
                testValue);

            sr.Verify(s => s.SetStorageValue(testAddress, testKey, testValue, testVersion));            

            // Test that gas is used
            Assert.True(gasMeter.GasConsumed < availableGas);
        }
    }
}

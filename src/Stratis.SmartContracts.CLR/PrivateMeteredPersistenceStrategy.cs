using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.CLR.Metering;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.CLR
{
    public class PrivateMeteredPersistenceStrategy : IPersistenceStrategy
    {
        private readonly IStateRepository stateDb;
        private readonly RuntimeObserver.IGasMeter gasMeter;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly IReadWriteSetOperations readWriteSet;

        private readonly string version;

        public PrivateMeteredPersistenceStrategy(IStateRepository stateDb,
            RuntimeObserver.IGasMeter gasMeter,
            IKeyEncodingStrategy keyEncodingStrategy,
            IReadWriteSetOperations readWriteSet,
            string version)
        {
            Guard.NotNull(stateDb, nameof(stateDb));
            Guard.NotNull(gasMeter, nameof(gasMeter));
            Guard.NotNull(gasMeter, nameof(keyEncodingStrategy));

            this.stateDb = stateDb;
            this.gasMeter = gasMeter;
            this.keyEncodingStrategy = keyEncodingStrategy;
            this.readWriteSet = readWriteSet;
            this.version = version;
        }

        public bool ContractExists(uint160 address)
        {
            throw new System.NotImplementedException();
        }

        public byte[] FetchBytes(uint160 address, byte[] key)
        {
            throw new System.NotImplementedException();
        }

        public void StoreBytes(uint160 address, byte[] key, byte[] value)
        {
            throw new System.NotImplementedException();
        }
    }
}
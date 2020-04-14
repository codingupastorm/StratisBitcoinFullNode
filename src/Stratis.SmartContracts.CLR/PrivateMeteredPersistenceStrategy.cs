using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.CLR.Metering;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Store;

namespace Stratis.SmartContracts.CLR
{
    public class PrivateMeteredPersistenceStrategy : IPersistenceStrategy
    {
        private readonly IPrivateDataStore stateDb;
        private readonly RuntimeObserver.IGasMeter gasMeter;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly IReadWriteSetOperations readWriteSet;

        private readonly string version;

        public PrivateMeteredPersistenceStrategy(IPrivateDataStore stateDb,
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
            this.gasMeter.Spend((RuntimeObserver.Gas)GasPriceList.StorageCheckContractExistsCost);

            // TODO remove this and consider redefining the interface for private persistent state.
            return false;
        }

        public byte[] FetchBytes(uint160 address, byte[] key)
        {
            byte[] encodedKey = this.keyEncodingStrategy.GetBytes(key);
            var rwsKey = new ReadWriteSetKey(address, key);

            if (this.readWriteSet.GetWriteItem(rwsKey, out var rwsValue))
            {
                return rwsValue;
            }

            var result = this.stateDb.GetBytes(address, encodedKey);

            var storageValue = StorageValue.FromBytes(result);

            this.readWriteSet.AddReadItem(new ReadWriteSetKey(address, key), storageValue.Version);

            RuntimeObserver.Gas operationCost = GasPriceList.StorageRetrieveOperationCost(encodedKey, storageValue.Value);
            this.gasMeter.Spend(operationCost);

            return storageValue.Value;
        }

        public void StoreBytes(uint160 address, byte[] key, byte[] value, bool isPrivateData = true)
        {
            byte[] encodedKey = this.keyEncodingStrategy.GetBytes(key);
            RuntimeObserver.Gas operationCost = GasPriceList.StorageSaveOperationCost(
                encodedKey,
                value);

            this.gasMeter.Spend(operationCost);

            this.readWriteSet.AddWriteItem(new ReadWriteSetKey(address, key), value, true);
        }
    }
}
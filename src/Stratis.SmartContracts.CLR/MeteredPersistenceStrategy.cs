using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core.Hashing;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.CLR
{
    public class PrivateReadWriteSetOperations : IReadWriteSetOperations
    {
        private readonly IReadWriteSetOperations publicReadWriteSet;
        private readonly IReadWriteSetOperations privateReadWriteSet;

        public PrivateReadWriteSetOperations(IReadWriteSetOperations publicReadWriteSet, IReadWriteSetOperations privateReadWriteSet)
        {
            this.publicReadWriteSet = publicReadWriteSet;
            this.privateReadWriteSet = privateReadWriteSet;
        }

        public void AddReadItem(ReadWriteSetKey key, string version)
        {
            this.publicReadWriteSet.AddReadItem(key, version);
            this.privateReadWriteSet.AddReadItem(key, version);
        }

        public void AddWriteItem(ReadWriteSetKey key, byte[] value)
        {
            this.publicReadWriteSet.AddWriteItem(key, HashHelper.Keccak256(value));
            this.privateReadWriteSet.AddWriteItem(key, value);
        }
    }

    /// <summary>
    /// Defines a data persistence strategy for a byte[] key value pair belonging to an address.
    /// Uses a GasMeter to perform accounting
    /// </summary>
    public class MeteredPersistenceStrategy : IPersistenceStrategy
    {
        private readonly IStateRepository stateDb;
        private readonly RuntimeObserver.IGasMeter gasMeter;
        private readonly IKeyEncodingStrategy keyEncodingStrategy;
        private readonly IReadWriteSetOperations readWriteSet;

        private readonly string version;

        public MeteredPersistenceStrategy(IStateRepository stateDb,
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

            return this.stateDb.IsExist(address);
        }

        public byte[] FetchBytes(uint160 address, byte[] key)
        {
            byte[] encodedKey = this.keyEncodingStrategy.GetBytes(key);
            StorageValue storageValue = this.stateDb.GetStorageValue(address, encodedKey);

            this.readWriteSet.AddReadItem(new ReadWriteSetKey(address, key), storageValue.Version);

            RuntimeObserver.Gas operationCost = GasPriceList.StorageRetrieveOperationCost(encodedKey, storageValue.Value);
            this.gasMeter.Spend(operationCost);

            return storageValue.Value;
        }

        public void StoreBytes(uint160 address, byte[] key, byte[] value)
        {
            byte[] encodedKey = this.keyEncodingStrategy.GetBytes(key);
            RuntimeObserver.Gas operationCost = GasPriceList.StorageSaveOperationCost(
                encodedKey,
                value);

            this.gasMeter.Spend(operationCost);
            this.stateDb.SetStorageValue(address, encodedKey, value, this.version);

            this.readWriteSet.AddWriteItem(new ReadWriteSetKey(address, key), value);
        }
    }
}
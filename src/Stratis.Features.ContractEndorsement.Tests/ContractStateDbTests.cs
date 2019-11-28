using System.Collections.Generic;
using NBitcoin;
using Stratis.Features.ContractEndorsement.State;
using Stratis.Patricia;
using Xunit;

namespace Stratis.Features.ContractEndorsement.Tests
{
    public class ContractStateDbTests
    {
        private readonly Database<uint160, ContractState> contractDb;
        private readonly ByteArrayDatabase<byte[]> codeDb;
        private readonly Database<CacheKey, StorageValue> contractStorageDb;
        private readonly FinalisedStateDb db;

        public ContractStateDbTests()
        {
            this.contractDb = new Database<uint160, ContractState>();
            this.codeDb = new ByteArrayDatabase<byte[]>(); 
            this.contractStorageDb = new Database<CacheKey, StorageValue>();
            this.db = new FinalisedStateDb(this.contractDb, this.codeDb, this.contractStorageDb);
        }

        [Fact]
        public void BasicOperations()
        {
            this.contractDb.Put(0, new ContractState
            {
                CodeHash = new byte[] {1,2,3},
                TypeName = "Test"
            });

            ContractState contractState = this.db.GetContractState(0);

            Assert.Equal("Test", contractState.TypeName);
        }
    }

    public class ByteArrayDatabase<V> : IDatabase<byte[], V>
    {
        private readonly Dictionary<byte[], V> db = new Dictionary<byte[], V>(new ByteArrayComparer());
        public V Get(byte[] key)
        {
            if (this.db.ContainsKey(key))
                return this.db[key];

            return default(V);
        }

        public void Put(byte[] key, V val)
        {
            this.db[key] = val;
        }
    }

    public class Database<K, V> : IDatabase<K, V>
    {
        private readonly Dictionary<K, V> db = new Dictionary<K, V>();
        public V Get(K key)
        {
            if (this.db.ContainsKey(key))
                return this.db[key];

            return default(V);
        }

        public void Put(K key, V val)
        {
            this.db[key] = val;
        }
    }
}

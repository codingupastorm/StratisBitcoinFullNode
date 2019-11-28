using System.Collections.Generic;
using NBitcoin;
using Stratis.Patricia;
using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.Features.ContractEndorsement.State
{
    /// <summary>
    /// Used during contract execution.
    /// </summary>
    public class CachedStateDb : IReadableStateDb, IWritableStateDb
    {
        /// <summary>
        /// When things are written during execution they go here for retrieval later on.
        /// </summary>
        private readonly Dictionary<CacheKey, StateValue> storageCache;

        private readonly Dictionary<uint160, ContractState> contractStateCache;

        private readonly Dictionary<byte[], byte[]> codeHashCache;

        /// <summary>
        /// The actual database.
        /// </summary>
        private readonly IReadableStateDb canonicalDb;

        public CachedStateDb(IReadableStateDb canonicalDb)
        {
            this.storageCache = new Dictionary<CacheKey, StateValue>();
            this.contractStateCache = new Dictionary<uint160, ContractState>();
            this.codeHashCache = new Dictionary<byte[], byte[]>(new ByteArrayComparer());
            this.canonicalDb = canonicalDb;
        }

        #region Setters

        public void SetState(uint160 contractAddress, string key, StateValue data)
        {
            this.storageCache[new CacheKey(contractAddress, key)] = data;
        }

        public void SetContractState(uint160 contractAddress, ContractState contractState)
        {
            this.contractStateCache[contractAddress] = contractState;
        }

        public void SetContractCode(uint160 contractAddress, byte[] code)
        {
            byte[] hash = HashHelper.Keccak256(code);

            this.codeHashCache[hash] = code;

            if (this.contractStateCache.ContainsKey(contractAddress))
            {
                this.contractStateCache[contractAddress].CodeHash = hash;
                return;
            }

            this.contractStateCache[contractAddress] = new ContractState
            {
                CodeHash = hash
            };
        }

        public void SetContractType(uint160 contractAddress, string typeName)
        {
            if (this.contractStateCache.ContainsKey(contractAddress))
            {
                this.contractStateCache[contractAddress].TypeName = typeName;
                return;
            }

            this.contractStateCache[contractAddress] = new ContractState
            {
                TypeName = typeName
            };
        }

        #endregion

        public bool IsExist(uint160 addr)
        {
            throw new System.NotImplementedException();
        }

        public ContractState GetContractState(uint160 addr)
        {
            throw new System.NotImplementedException();
        }

        public byte[] GetCode(uint160 addr)
        {
            throw new System.NotImplementedException();
        }

        public byte[] GetCodeHash(uint160 addr)
        {
            throw new System.NotImplementedException();
        }

        public string GetContractType(uint160 addr)
        {
            throw new System.NotImplementedException();
        }

        public StateValue GetState(uint160 contractAddress, string key)
        {
            var cacheKey = new CacheKey(contractAddress, key);

            if (this.storageCache.ContainsKey(cacheKey))
                return this.storageCache[cacheKey];

            return this.canonicalDb.GetState(contractAddress, key);
        }
    }
}

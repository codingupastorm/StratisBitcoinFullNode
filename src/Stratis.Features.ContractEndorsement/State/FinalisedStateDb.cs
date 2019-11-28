using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Features.ContractEndorsement.ReadWrite;

namespace Stratis.Features.ContractEndorsement.State
{
    public class FinalisedStateDb : IReadableStateDb
    {
        private readonly IDatabase<uint160, ContractState> contractStateDb;

        private readonly IDatabase<byte[], byte[]> codeHashDb;

        private readonly IDatabase<CacheKey, StorageValue> contractStorageDatabase;

        public FinalisedStateDb(IDatabase<uint160, ContractState> contractStateDb,
            IDatabase<byte[], byte[]> codeHashDb,
            IDatabase<CacheKey, StorageValue> contractStorageDatabase)
        {
            this.contractStateDb = contractStateDb;
            this.codeHashDb = codeHashDb;
            this.contractStorageDatabase = contractStorageDatabase;
        }


        public bool IsExist(uint160 addr)
        {
            return this.contractStateDb.Get(addr) != null;
        }

        public ContractState GetContractState(uint160 addr)
        {
            return this.contractStateDb.Get(addr);
        }

        public byte[] GetCode(uint160 addr)
        {
            ContractState contractState = this.contractStateDb.Get(addr);

            if (contractState == null)
                return null;

            return this.codeHashDb.Get(contractState.CodeHash);
        }

        public byte[] GetCodeHash(uint160 addr)
        {
            ContractState contractState = this.contractStateDb.Get(addr);
            return contractState?.CodeHash;
        }

        public string GetContractType(uint160 addr)
        {
            ContractState contractState = this.contractStateDb.Get(addr);
            return contractState?.TypeName;
        }

        public StorageValue GetStorageValue(uint160 contractAddress, string key)
        {
            return this.contractStorageDatabase.Get(new CacheKey(contractAddress, key));
        }

        /// <summary>
        ///  Used to push a write set to the database.
        /// </summary>
        /// <param name="writeSet"></param>
        public void ApplyWriteSet(List<Write> writeSet)
        {
            throw new NotImplementedException();
        }

    }
}

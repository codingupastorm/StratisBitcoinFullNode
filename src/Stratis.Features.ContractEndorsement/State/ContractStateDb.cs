using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Features.ContractEndorsement.ReadWrite;

namespace Stratis.Features.ContractEndorsement.State
{
    public class ContractStateDb
    {
        private readonly IDatabase<uint160, ContractState> contractStateDb;

        private readonly IDatabase<byte[], byte[]> codeHashDb;

        private readonly IDatabase<CacheKey, StateValue> contractStorageDatabase;

        public ContractStateDb(IDatabase<uint160, ContractState> contractStateDb,
            IDatabase<byte[], byte[]> codeHashDb,
            IDatabase<CacheKey, StateValue> contractStorageDatabase)
        {
            this.contractStateDb = contractStateDb;
            this.codeHashDb = codeHashDb;
            this.contractStorageDatabase = contractStorageDatabase;
        }


        public bool IsExist(uint160 addr)
        {
            throw new NotImplementedException();
        }

        public ContractState GetContractState(uint160 addr)
        {
            throw new NotImplementedException();
        }

        public byte[] GetCode(uint160 addr)
        {
            throw new NotImplementedException();
        }

        public byte[] GetCodeHash(uint160 addr)
        {
            throw new NotImplementedException();
        }

        public string GetContractType(uint160 addr)
        {
            throw new NotImplementedException();
        }

        public void SetState(uint160 contractAddress, string key, StateValue data)
        {
            throw new NotImplementedException();
        }

        public StateValue GetState(uint160 contractAddress, string key)
        {
            throw new NotImplementedException();
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

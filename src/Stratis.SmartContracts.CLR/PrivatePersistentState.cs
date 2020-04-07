using System;
using NBitcoin;
using Stratis.SmartContracts.Core.Hashing;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.SmartContracts.CLR
{
    public class PrivatePersistentState : IPersistentState
    {
        private readonly ReadWriteSetBuilder rwsBuilder;
        private readonly ISerializer serializer;
        private readonly IPersistenceStrategy persistenceStrategy;
        private readonly uint160 contractAddress;

        public PrivatePersistentState(
            ISerializer serializer,
            IPersistenceStrategy persistenceStrategy,
            uint160 contractAddress,
            ReadWriteSetBuilder readWriteSetBuilder)
        {
            this.serializer = serializer;
            this.persistenceStrategy = persistenceStrategy;
            this.contractAddress = contractAddress;
            this.rwsBuilder = readWriteSetBuilder;
        }

        public ReadWriteSet GetReadWriteSet()
        {
            return this.rwsBuilder.GetReadWriteSet();
        }

        public void SetBytes(byte[] key, byte[] value)
        {
            // Keep the bytes in a readwrite set for now. 
            this.rwsBuilder.AddWriteItem(new ReadWriteSetKey(this.contractAddress, key), value);

            // Store a hash of the bytes in the normal data store.
            byte[] hash = HashHelper.Keccak256(value);
            this.persistenceStrategy.StoreBytes(this.contractAddress, key, hash, true);
        }

        public bool IsContract(Address address)
        {
            byte[] serialized = this.serializer.Serialize(address);

            if (serialized == null)
            {
                return false;
            }

            var contractAddress = new uint160(serialized);

            return this.persistenceStrategy.ContractExists(contractAddress);
        }

        public byte[] GetBytes(byte[] key)
        {
            throw new NotImplementedException("This is not getting private data from the correct place.");
            byte[] bytes = this.persistenceStrategy.FetchBytes(this.contractAddress, key);

            if (bytes == null)
                return new byte[0];

            return bytes;
        }

        public byte[] GetBytes(string key)
        {
            byte[] keyBytes = this.serializer.Serialize(key);

            return this.GetBytes(keyBytes);
        }

        public char GetChar(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.serializer.ToChar(bytes);
        }

        public Address GetAddress(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.serializer.ToAddress(bytes);
        }

        public bool GetBool(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.serializer.ToBool(bytes);
        }

        public int GetInt32(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.serializer.ToInt32(bytes);
        }

        public uint GetUInt32(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.serializer.ToUInt32(bytes);
        }

        public long GetInt64(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.serializer.ToInt64(bytes);
        }

        public ulong GetUInt64(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.serializer.ToUInt64(bytes);
        }

        public string GetString(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.serializer.ToString(bytes);
        }

        public T GetStruct<T>(string key) where T : struct
        {
            byte[] bytes = this.GetBytes(key);

            return this.serializer.ToStruct<T>(bytes);
        }

        public T[] GetArray<T>(string key)
        {
            byte[] bytes = this.GetBytes(key);

            return this.serializer.ToArray<T>(bytes);
        }

        public void SetBytes(string key, byte[] value)
        {
            byte[] keyBytes = this.serializer.Serialize(key);

            this.SetBytes(keyBytes, value);
        }

        public void SetChar(string key, char value)
        {
            this.SetBytes(key, this.serializer.Serialize(value));
        }

        public void SetAddress(string key, Address value)
        {
            this.SetBytes(key, this.serializer.Serialize(value));
        }

        public void SetBool(string key, bool value)
        {
            this.SetBytes(key, this.serializer.Serialize(value));
        }

        public void SetInt32(string key, int value)
        {
            this.SetBytes(key, this.serializer.Serialize(value));
        }

        public void SetUInt32(string key, uint value)
        {
            this.SetBytes(key, this.serializer.Serialize(value));
        }

        public void SetInt64(string key, long value)
        {
            this.SetBytes(key, this.serializer.Serialize(value));
        }

        public void SetUInt64(string key, ulong value)
        {
            this.SetBytes(key, this.serializer.Serialize(value));
        }

        public void SetString(string key, string value)
        {
            this.SetBytes(key, this.serializer.Serialize(value));
        }

        public void SetArray(string key, Array a)
        {
            this.SetBytes(key, this.serializer.Serialize(a));
        }

        public void SetStruct<T>(string key, T value) where T : struct
        {
            this.SetBytes(key, this.SerializeStruct(value));
        }

        private byte[] SerializeStruct<T>(T value) where T : struct
        {
            return this.serializer.Serialize(value);
        }

        public void Clear(string key)
        {
            this.SetBytes(key, null);
        }
    }
}

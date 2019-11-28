using System;
using Stratis.Features.ContractEndorsement.ReadWrite;
using Stratis.SmartContracts;

namespace Stratis.Features.ContractEndorsement
{
    public class ReadWriteTrackingPersistentState : IPersistentState
    {
        private readonly ReadWriteSet readWriteSet;

        public ReadWriteTrackingPersistentState(ReadWriteSet readWriteSet)
        {
            this.readWriteSet = readWriteSet;
            // TODO: Reads or writes with 
        }

        public bool IsContract(Address address)
        {
            throw new NotImplementedException();
        }

        public byte[] GetBytes(byte[] key)
        {
            throw new NotImplementedException();
        }

        public byte[] GetBytes(string key)
        {
            throw new NotImplementedException();
        }

        public char GetChar(string key)
        {
            throw new NotImplementedException();
        }

        public Address GetAddress(string key)
        {
            throw new NotImplementedException();
        }

        public bool GetBool(string key)
        {
            throw new NotImplementedException();
        }

        public int GetInt32(string key)
        {
            throw new NotImplementedException();
        }

        public uint GetUInt32(string key)
        {
            throw new NotImplementedException();
        }

        public long GetInt64(string key)
        {
            throw new NotImplementedException();
        }

        public ulong GetUInt64(string key)
        {
            throw new NotImplementedException();
        }

        public string GetString(string key)
        {
            throw new NotImplementedException();
        }

        public T GetStruct<T>(string key) where T : struct
        {
            throw new NotImplementedException();
        }

        public T[] GetArray<T>(string key)
        {
            throw new NotImplementedException();
        }

        public void SetBytes(byte[] key, byte[] value)
        {
            throw new NotImplementedException();
        }

        public void SetBytes(string key, byte[] value)
        {
            throw new NotImplementedException();
        }

        public void SetChar(string key, char value)
        {
            throw new NotImplementedException();
        }

        public void SetAddress(string key, Address value)
        {
            throw new NotImplementedException();
        }

        public void SetBool(string key, bool value)
        {
            throw new NotImplementedException();
        }

        public void SetInt32(string key, int value)
        {
            throw new NotImplementedException();
        }

        public void SetUInt32(string key, uint value)
        {
            throw new NotImplementedException();
        }

        public void SetInt64(string key, long value)
        {
            throw new NotImplementedException();
        }

        public void SetUInt64(string key, ulong value)
        {
            throw new NotImplementedException();
        }

        public void SetString(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void SetStruct<T>(string key, T value) where T : struct
        {
            throw new NotImplementedException();
        }

        public void SetArray(string key, Array a)
        {
            throw new NotImplementedException();
        }

        public void Clear(string key)
        {
            throw new NotImplementedException();
        }
    }
}

using System;
using System.Numerics;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Core.ReadWrite
{
    public struct ReadWriteSetKey
    {
        public uint160 ContractAddress { get; }
        public byte[] Key { get; }

        public ReadWriteSetKey(uint160 contractAddress, byte[] key)
        {
            this.ContractAddress = contractAddress;
            byte[] clonedKey = new byte[key.Length];
            Array.Copy(key, clonedKey, key.Length);
            this.Key = clonedKey;
        }

        // TODO: These may be slow.

        public override bool Equals(object obj)
        {
            if (!(obj is ReadWriteSetKey))
                return false;

            ReadWriteSetKey other = (ReadWriteSetKey)obj;
            return Equals(this.ContractAddress, other.ContractAddress) && new ByteArrayComparer().Equals(this.Key, other.Key);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((this.ContractAddress != null ? this.ContractAddress.GetHashCode() : 0) * 397) ^ (this.Key != null ? new BigInteger(this.Key).GetHashCode() : 0);
            }
        }
    }
}

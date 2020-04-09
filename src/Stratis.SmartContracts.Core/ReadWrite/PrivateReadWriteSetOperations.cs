using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.Core.ReadWrite
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

        public bool GetWriteItem(ReadWriteSetKey key, out byte[] value)
        {
            return this.privateReadWriteSet.GetWriteItem(key, out value);
        }
    }
}
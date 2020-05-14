using Stratis.SmartContracts.Core.State;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests
{
    public class StorageValueTests
    {
        [Fact]
        public void CanSerializeAndDeserializeStorageValue()
        {
            var storageValue = new StorageValue(new byte[] {0, 1, 2, 3}, "0.0");
            byte[] serialized = storageValue.ToBytes();
            StorageValue deserialized = StorageValue.FromBytes(serialized);
            Assert.Equal(storageValue.Value, deserialized.Value);
            Assert.Equal(storageValue.Version, deserialized.Version);
        }
    }
}

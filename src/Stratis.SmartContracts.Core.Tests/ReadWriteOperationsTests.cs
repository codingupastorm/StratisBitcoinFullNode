using Moq;
using NBitcoin;
using Stratis.SmartContracts.Core.Hashing;
using Stratis.SmartContracts.Core.ReadWrite;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests
{
    public class ReadWriteOperationsTests
    {
        [Fact]
        public void PrivateReadWriteSetOperations_AddReadItem_Specification()
        {
            var publicRws = new Mock<IReadWriteSetOperations>();
            var privateRws = new Mock<IReadWriteSetOperations>();

            var operations = new PrivateReadWriteSetOperations(publicRws.Object, privateRws.Object);
            var key = new ReadWriteSetKey(uint160.One, new byte[] { 0xAA, 0xBB, 0xCC });
            var version = "1.1";
    
            operations.AddReadItem(key, version);

            publicRws.Verify(rws => rws.AddReadItem(key, version), Times.Once);
            privateRws.Verify(rws => rws.AddReadItem(key, version), Times.Once);
        }

        [Fact]
        public void PrivateReadWriteSetOperations_AddWriteItem_Specification()
        {
            var publicRws = new Mock<IReadWriteSetOperations>();
            var privateRws = new Mock<IReadWriteSetOperations>();

            var operations = new PrivateReadWriteSetOperations(publicRws.Object, privateRws.Object);
            
            var key = new ReadWriteSetKey(uint160.One, new byte[] { 0xAA, 0xBB, 0xCC });
            var value = new byte[] {0xAA, 0xBB, 0xCC};
            var hashedValue = HashHelper.Keccak256(value);

            operations.AddWriteItem(key, value);

            publicRws.Verify(rws => rws.AddWriteItem(key, hashedValue, true), Times.Once);
            privateRws.Verify(rws => rws.AddWriteItem(key, value, false), Times.Once);
        }
    }
}
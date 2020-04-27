using System.Linq;
using System.Text;
using Moq;
using NBitcoin;
using Stratis.Patricia;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.State;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests
{
    public class ReadWriteSetValidatorTests
    {
        private static readonly uint160 TestAddress = uint160.One;
        private static readonly byte[] Key1Bytes = Encoding.UTF8.GetBytes("key1");
        private static readonly byte[] Key2Bytes = Encoding.UTF8.GetBytes("key2");
        private const string Version1 = "1.1";
        private const string Version2 = "1.2";
        private static byte[] Value1 = new byte[] { 0, 1, 2, 3 };
        private static readonly byte[] Value2 = new byte[] { 4, 5, 6, 7 };

        [Fact]
        public void AllCorrectVersionsReturnsTrue()
        {
            // Set up our current state to be all version 1.
            var stateRepoMock = new Mock<IStateRepository>();
            stateRepoMock.Setup(x => x.GetStorageValue(TestAddress, Key1Bytes))
                .Returns(new StorageValue(Key1Bytes, Version1));
            stateRepoMock.Setup(x => x.GetStorageValue(TestAddress, Key2Bytes))
                .Returns(new StorageValue(Key2Bytes, Version1));

            // Add some reads which use version 1.
            var readWriteSetBuilder = new ReadWriteSetBuilder();
            readWriteSetBuilder.AddReadItem(new ReadWriteSetKey(TestAddress, Key1Bytes), Version1);
            readWriteSetBuilder.AddReadItem(new ReadWriteSetKey(TestAddress, Key2Bytes), Version1);

            ReadWriteSet readWriteSet = readWriteSetBuilder.GetReadWriteSet();

            var validator = new ReadWriteSetValidator();
            Assert.True(validator.IsReadWriteSetValid(stateRepoMock.Object, readWriteSet));
        }

        [Fact]
        public void AnIncorrectVersionReturnsFalse()
        {
            // Set up our current state to have one version 2. Imagine this was updated in a block before this RWS came in.
            var stateRepoMock = new Mock<IStateRepository>();
            stateRepoMock.Setup(x => x.GetStorageValue(TestAddress, Key1Bytes))
                .Returns(new StorageValue(Key1Bytes, Version1));
            stateRepoMock.Setup(x => x.GetStorageValue(TestAddress, Key2Bytes))
                .Returns(new StorageValue(Key2Bytes, Version2));

            // Add some reads which use version 1.
            var readWriteSetBuilder = new ReadWriteSetBuilder();
            readWriteSetBuilder.AddReadItem(new ReadWriteSetKey(TestAddress, Key1Bytes), Version1);
            readWriteSetBuilder.AddReadItem(new ReadWriteSetKey(TestAddress, Key2Bytes), Version1);

            ReadWriteSet readWriteSet = readWriteSetBuilder.GetReadWriteSet();

            var validator = new ReadWriteSetValidator();
            Assert.False(validator.IsReadWriteSetValid(stateRepoMock.Object, readWriteSet));
        }

        [Fact]
        public void ANullReturnsFalse()
        {
            // Set up our current state to have one version 2. Imagine this was updated in a block before this RWS came in.
            var stateRepoMock = new Mock<IStateRepository>();
            stateRepoMock.Setup(x => x.GetStorageValue(TestAddress, Key1Bytes))
                .Returns(new StorageValue(Key1Bytes, Version1));
            stateRepoMock.Setup(x => x.GetStorageValue(TestAddress, Key2Bytes))
                .Returns((StorageValue)null);

            // Add some reads which use version 1.
            var readWriteSetBuilder = new ReadWriteSetBuilder();
            readWriteSetBuilder.AddReadItem(new ReadWriteSetKey(TestAddress, Key1Bytes), Version1);
            readWriteSetBuilder.AddReadItem(new ReadWriteSetKey(TestAddress, Key2Bytes), Version1);

            ReadWriteSet readWriteSet = readWriteSetBuilder.GetReadWriteSet();

            var validator = new ReadWriteSetValidator();
            Assert.False(validator.IsReadWriteSetValid(stateRepoMock.Object, readWriteSet));
        }

        [Fact]
        public void AppliesToStateRepo()
        {
            // Empty repo
            StateRepositoryRoot repository = new StateRepositoryRoot(new MemoryDictionarySource());

            // Some writes in a read write set
            var readWriteSetBuilder = new ReadWriteSetBuilder();
            readWriteSetBuilder.AddWriteItem(new ReadWriteSetKey(TestAddress, Key1Bytes), Value1);
            readWriteSetBuilder.AddWriteItem(new ReadWriteSetKey(TestAddress, Key2Bytes), Value2);
            ReadWriteSet readWriteSet = readWriteSetBuilder.GetReadWriteSet();

            var validator = new ReadWriteSetValidator();

            validator.ApplyReadWriteSet(repository, readWriteSet, Version1);

            Assert.Equal(Value1, repository.GetStorageValue(TestAddress, Key1Bytes).Value);
            Assert.Equal(Value2, repository.GetStorageValue(TestAddress, Key2Bytes).Value);
        }

        [Fact]
        public void ValidatePublicRwsWithDifferentHashFails()
        {
            var publicReadWriteSetBuilder = new ReadWriteSetBuilder();
            var privateReadWriteSetBuilder = new ReadWriteSetBuilder();

            // Use this to make it easier to build the initial two RWS.
            var operations = new PrivateReadWriteSetOperations(publicReadWriteSetBuilder, privateReadWriteSetBuilder);
            
            operations.AddWriteItem(new ReadWriteSetKey(TestAddress, Key1Bytes), Value1);
            operations.AddWriteItem(new ReadWriteSetKey(TestAddress, Key2Bytes), Value2);

            var publicRws = publicReadWriteSetBuilder.GetReadWriteSet();
            var privateRws = privateReadWriteSetBuilder.GetReadWriteSet();

            // Should be the same at the moment.
            Assert.True(ReadWriteSetValidator.ValidatePublicReadWriteSet(publicRws, privateRws));

            // Now change some data in the public RWS
            publicReadWriteSetBuilder = new ReadWriteSetBuilder();
            publicReadWriteSetBuilder.AddWriteItem(new ReadWriteSetKey(TestAddress, Key1Bytes), Value1);
            publicReadWriteSetBuilder.AddWriteItem(new ReadWriteSetKey(TestAddress, Key2Bytes), Value1); // Subtle change

            publicRws = publicReadWriteSetBuilder.GetReadWriteSet();

            Assert.False(ReadWriteSetValidator.ValidatePublicReadWriteSet(publicRws, privateRws));
        }
    }
}

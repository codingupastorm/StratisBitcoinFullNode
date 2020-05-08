using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Configuration;
using Stratis.Bitcoin.Tests.Common;
using Stratis.SmartContracts.Core.Store;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests.Store
{
    public class PrivateDataStoreIntegrationTests : TestBase, IDisposable
    {
        private readonly string dir;
        private readonly PrivateDataKeyValueStore repo;
        private readonly PrivateDataStore store;

        public PrivateDataStoreIntegrationTests()
            : base(KnownNetworks.Main)
        {
            this.dir = CreateTestDir(this);
            this.repo = new PrivateDataKeyValueStore(new DataFolder(this.dir), new LoggerFactory(), this.RepositorySerializer);
            this.store = new PrivateDataStore(this.repo);
        }

        [Fact]
        public void Insert_Replaces_Existing_Value()
        {
            var contractAddress = new uint160(RandomUtils.GetBytes(20));
            var key = RandomUtils.GetBytes(64);
            var value = RandomUtils.GetBytes(128);
            
            this.store.StoreBytes(contractAddress, key, value);

            var data = this.store.GetBytes(contractAddress, key);

            Assert.True(value.SequenceEqual(data));

            var newValue = value.ToArray();
            newValue[0] = (byte) (newValue[0] + 1); // Slightly change the original value.

            this.store.StoreBytes(contractAddress, key, newValue);

            var data2 = this.store.GetBytes(contractAddress, key);

            Assert.False(value.SequenceEqual(data2));
            Assert.True(newValue.SequenceEqual(data2));
        }

        public void Dispose()
        {
            this.repo.Dispose();
            Directory.Delete(this.dir, true);
        }
    }
}
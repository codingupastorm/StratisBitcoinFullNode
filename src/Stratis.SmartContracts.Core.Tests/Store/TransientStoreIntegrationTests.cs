using System;
using System.IO;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.SmartContracts.Core.Store;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests.Store
{
    public class TransientStoreIntegrationTests : TestBase, IDisposable
    {
        private TransientStore store;
        private TransientKeyValueStore repo;
        private string dir;

        public TransientStoreIntegrationTests() 
            : base(KnownNetworks.Main)
        {
            this.dir = CreateTestDir(this);
            this.repo = new TransientKeyValueStore(new DataFolder(this.dir), new LoggerFactory(), this.RepositorySerializer);

            this.store = new TransientStore(this.repo);
        }

        public void Dispose()
        {
            this.repo.Dispose();
            Directory.Delete(this.dir, true);
        }

        [Fact]
        public void GetMinBlockHeight_Success()
        {
            Assert.Equal(0UL, this.store.GetMinBlockHeight());

            this.store.Persist(uint256.One, 100, new TransientStorePrivateData(new byte[] {}));

            Assert.Equal(100UL, this.store.GetMinBlockHeight());

            this.store.Persist(uint256.Zero, 50, new TransientStorePrivateData(new byte[] {}));

            Assert.Equal(50UL, this.store.GetMinBlockHeight());
        }

        [Fact]
        public void PurgeBelowHeight_Success()
        {
            var recordsToAdd = 100;
            uint blockHeightToPurge = 50;

            // Add some fake data
            for (uint i = 0; i < recordsToAdd; i++)
            {
                this.store.Persist(new uint256(i), i, new TransientStorePrivateData(new byte[] { }));
            }

            Assert.Equal(0U, this.store.GetMinBlockHeight());

            // Check that the values exist.
            using (var tx = this.repo.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                // n data + n index + 1 min block height
                Assert.Equal(recordsToAdd * 2 + 1, tx.Count(TransientStore.Table));
            }

            // Attempt to purge everything below 50.
            this.store.PurgeBelowHeight(blockHeightToPurge);

            var remainingRecords = recordsToAdd - blockHeightToPurge;

            using (var tx = this.repo.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                Assert.Equal(remainingRecords * 2 + 1, tx.Count(TransientStore.Table));
            }

            Assert.Equal(blockHeightToPurge, this.store.GetMinBlockHeight());
        }

        [Fact]
        public void PurgeById_Success()
        {
            var recordsToAdd = 100;
            var recordIdToPurge = new uint256(50);

            // Add some fake data
            for (uint i = 0; i < recordsToAdd; i++)
            {
                this.store.Persist(new uint256(i), i, new TransientStorePrivateData(new byte[] { }));
            }

            Assert.Equal(0U, this.store.GetMinBlockHeight());

            // Check that the values exist.
            using (var tx = this.repo.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                // n data + n index + 1 min block height
                Assert.Equal(recordsToAdd * 2 + 1, tx.Count(TransientStore.Table));
            }

            // Attempt to purge the singular record.
            this.store.Purge(recordIdToPurge);

            var remainingRecords = recordsToAdd - 1;

            using (var tx = this.repo.CreateTransaction(KeyValueStoreTransactionMode.Read))
            {
                Assert.Equal(remainingRecords * 2 + 1, tx.Count(TransientStore.Table));
            }

            var data = this.store.Get(recordIdToPurge);

            Assert.Null(data.Data);
            Assert.Equal(0U, data.BlockHeight);
        }

        [Fact]
        public void GetAndSetPrivateData()
        {
            uint256 id = new uint256(123456);
            uint blockHeight = 6789;
            TransientStorePrivateData data = new TransientStorePrivateData(new byte[] {0, 1, 2, 3});

            this.store.Persist(id, blockHeight, data);

            TransientStorePrivateData result = this.store.Get(id).Data;
            Assert.NotNull(result);
        }
    }
}
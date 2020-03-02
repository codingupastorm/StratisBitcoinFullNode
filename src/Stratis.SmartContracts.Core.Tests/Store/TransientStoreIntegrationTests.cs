using System.IO;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common;
using Stratis.SmartContracts.Core.Store;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests.Store
{
    public class TransientStoreIntegrationTests : TestBase
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
            Directory.Delete(this.dir);
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
    }
}
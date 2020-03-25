using Moq;
using Stratis.SmartContracts.Core.Store;

namespace Stratis.SmartContracts.Core.Tests.Store
{
    public class PrivateDataStoreTests
    {
        private PrivateDataStore store;
        private Mock<IPrivateDataKeyValueStore> keyValueStore;

        public PrivateDataStoreTests()
        {
            this.keyValueStore = new Mock<IPrivateDataKeyValueStore>();
            this.store = new PrivateDataStore(this.keyValueStore.Object);
        }
    }
}
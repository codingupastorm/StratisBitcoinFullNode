using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using Stratis.SmartContracts.Core.Store;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests.Store
{
    public class TransientStoreTests
    {
        private TransientStore store;
        private Mock<ITransientKeyValueStore> repository;

        public TransientStoreTests()
        {
            this.repository = new Mock<ITransientKeyValueStore>();
            this.store = new TransientStore(this.repository.Object);
        }

        [Fact]
        public void Persist_Data()
        {
            this.store.Per
        }
    }
}

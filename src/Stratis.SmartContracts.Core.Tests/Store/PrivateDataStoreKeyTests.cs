using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Core.Store;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests.Store
{
    public class PrivateDataStoreKeyTests
    {
        [Fact]
        public void CompositeKeyCreation()
        {
            var contract = uint160.One;
            var key = new byte[] { 0xAA, 0xBB, 0xCC };

            var compositeKey = PrivateDataStoreQueryParams.CreateCompositeKeyForContract(contract, key);

            var splitCompositeKeyForContract = PrivateDataStoreQueryParams.SplitCompositeKeyForContract(compositeKey);

            Assert.Equal(contract, splitCompositeKeyForContract.contractAddress);
            Assert.True(key.SequenceEqual(splitCompositeKeyForContract.key));
        }

    }
}
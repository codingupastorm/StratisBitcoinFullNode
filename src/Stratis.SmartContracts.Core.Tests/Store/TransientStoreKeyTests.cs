using System;
using NBitcoin;
using Stratis.SmartContracts.Core.Store;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests.Store
{
    public class TransientStoreKeyTests
    {
        [Fact]
        public void PurgeIndexByHeight_GreaterThan_LessThan_EqualTo()
        {
            // Create a purge index key at height 100.
            var purgeIndexKey = TransientStoreQueryParams.CreateCompositeKeyForPurgeIndexByHeight(100, uint256.One, Guid.NewGuid());

            // Create a query key starting from 0.
            var purgeIndexStartKey = TransientStoreQueryParams.CreatePurgeIndexByHeightRangeStartKey(0);
            var purgeIndexEndKey = TransientStoreQueryParams.CreatePurgeIndexByHeightRangeEndKey(100 - 10);

            // Check that the purge index key is greater than the start key.
            Assert.True(TransientStoreQueryParams.GreaterThan(purgeIndexKey, purgeIndexStartKey));
            Assert.True(TransientStoreQueryParams.LessThan(purgeIndexEndKey, purgeIndexKey));
            Assert.True(TransientStoreQueryParams.EqualTo(purgeIndexKey, purgeIndexKey));
        }
    }
}
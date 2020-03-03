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

            // The key should not be less than the end key
            Assert.False(TransientStoreQueryParams.LessThan(purgeIndexKey, purgeIndexEndKey));
            Assert.True(TransientStoreQueryParams.EqualTo(purgeIndexKey, purgeIndexKey));
        }

        [Fact]
        public void SplitCompositeKeyOfPurgeIndexByHeight_Success()
        {
            var height = 100U;
            var txId = uint256.One;
            var guid = Guid.NewGuid();
            var purgeIndexKey = TransientStoreQueryParams.CreateCompositeKeyForPurgeIndexByHeight(height, txId, guid);

            var split = TransientStoreQueryParams.SplitCompositeKeyOfPurgeIndexByHeight(purgeIndexKey);

            Assert.Equal(height, split.blockHeight);
            Assert.Equal(txId, split.txId);
            Assert.Equal(guid, split.uuid);
        }
    }
}
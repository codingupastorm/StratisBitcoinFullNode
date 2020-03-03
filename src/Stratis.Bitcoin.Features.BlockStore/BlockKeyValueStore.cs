using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockKeyValueStore : IKeyValueStoreRepository
    {
    }

    public class BlockKeyValueStore : KeyValueStoreLevelDB.KeyValueStoreLevelDB, IBlockKeyValueStore
    {
        public BlockKeyValueStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.BlockPath, loggerFactory, repositorySerializer)
        {
        }
    }
}

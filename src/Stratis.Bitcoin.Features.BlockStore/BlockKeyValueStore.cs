using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockKeyValueStore : IKeyValueStore
    {
    }

    public class BlockKeyValueStore : KeyValueStore<KeyValueStoreLevelDB.KeyValueStoreLevelDB>, IBlockKeyValueStore
    {
        public BlockKeyValueStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            :base(dataFolder.BlockPath, loggerFactory, repositorySerializer)
        {
        }
    }
}

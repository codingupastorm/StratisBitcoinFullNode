using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Core.AsyncWork;

namespace Stratis.Features.BlockStore
{
    public interface IBlockKeyValueStore : IKeyValueStoreRepository
    {
    }

    public class BlockKeyValueStore : Bitcoin.KeyValueStoreLevelDB.KeyValueStoreLevelDB, IBlockKeyValueStore
    {
        public BlockKeyValueStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.BlockPath, loggerFactory, repositorySerializer)
        {
        }
    }
}

using Microsoft.Extensions.Logging;
using Stratis.Core.Configuration;
using Stratis.Core.Interfaces;
using Stratis.Core.NodeStorage.KeyValueStoreLevelDB;
using Stratis.Core.Utilities;

namespace Stratis.Features.BlockStore
{
    public interface IBlockKeyValueStore : IKeyValueStoreRepository
    {
    }

    public class BlockKeyValueStore : KeyValueStoreLevelDB, IBlockKeyValueStore
    {
        public BlockKeyValueStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.BlockPath, loggerFactory, repositorySerializer)
        {
        }
    }
}

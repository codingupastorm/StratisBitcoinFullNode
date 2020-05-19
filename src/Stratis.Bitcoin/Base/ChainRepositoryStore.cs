using Microsoft.Extensions.Logging;
using Stratis.Core.Configuration;
using Stratis.Core.Interfaces;
using Stratis.Core.NodeStorage.KeyValueStoreLevelDB;
using Stratis.Core.Utilities;

namespace Stratis.Core.Base
{
    public interface IChainRepositoryStore : IKeyValueStore
    {
    }

    public class ChainRepositoryStore : KeyValueStoreLevelDB, IChainRepositoryStore
    {
        public ChainRepositoryStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.ChainPath, loggerFactory, repositorySerializer)
        {
        }
    }
}
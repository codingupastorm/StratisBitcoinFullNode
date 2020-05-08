using Microsoft.Extensions.Logging;
using Stratis.Core.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStoreLevelDB;
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
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Core.Utilities;

namespace Stratis.Bitcoin.Base
{
    public interface IChainRepositoryStore : IKeyValueStore
    {
    }

    public class ChainRepositoryStore : KeyValueStoreLevelDB.KeyValueStoreLevelDB, IChainRepositoryStore
    {
        public ChainRepositoryStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.ChainPath, loggerFactory, repositorySerializer)
        {
        }
    }
}
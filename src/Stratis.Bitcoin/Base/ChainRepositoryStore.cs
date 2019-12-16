using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base
{
    public interface IChainRepositoryStore : IKeyValueStore
    {
    }

    public class ChainRepositoryStore : KeyValueStore<KeyValueStoreLevelDB.KeyValueStoreLevelDB>, IChainRepositoryStore
    {
        public ChainRepositoryStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.ChainPath, loggerFactory, dateTimeProvider, repositorySerializer)
        {
        }
    }
}
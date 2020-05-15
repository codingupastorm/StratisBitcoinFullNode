using Microsoft.Extensions.Logging;
using Stratis.Core.Configuration;
using Stratis.Core.Interfaces;
using Stratis.Core.Utilities;

namespace Stratis.Features.Consensus
{
    public interface IDBCoinViewStore : IKeyValueStore
    {
    }

    public class DBCoinViewStore : Bitcoin.KeyValueStoreLevelDB.KeyValueStoreLevelDB, IDBCoinViewStore
    {
        public DBCoinViewStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.CoinViewPath, loggerFactory, repositorySerializer)
        {
        }
    }
}

using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Core.AsyncWork;

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

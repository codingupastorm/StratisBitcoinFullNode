using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;

namespace Stratis.Bitcoin.Features.Consensus
{
    public interface IDBCoinViewStore : IKeyValueStore
    {
    }

    public class DBCoinViewStore : KeyValueStore<KeyValueStoreLevelDB.KeyValueStoreLevelDB>, IDBCoinViewStore
    {
        public DBCoinViewStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.CoinViewPath, loggerFactory, dateTimeProvider, repositorySerializer)
        {
        }
    }
}

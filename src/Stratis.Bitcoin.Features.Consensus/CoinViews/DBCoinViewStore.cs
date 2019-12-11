using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.NodeStorage.Interfaces;
using Stratis.Features.NodeStorage.KeyValueStore;
using Stratis.Features.NodeStorage.KeyValueStoreLevelDB;

namespace Stratis.Bitcoin.Features.Consensus
{
    public interface IDBCoinViewStore : IKeyValueStore
    {
    }

    public class DBCoinViewStore : KeyValueStore<KeyValueStoreLevelDB>, IDBCoinViewStore
    {
        public DBCoinViewStore(Network network, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.CoinViewPath, loggerFactory, dateTimeProvider, new DBreezeSerializer(network.Consensus.ConsensusFactory))
        {
        }
    }
}

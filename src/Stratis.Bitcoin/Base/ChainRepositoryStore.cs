using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base
{ 
    public interface IChainRepositoryStore : IKeyValueStore
    {
    }

    public class ChainRepositoryStore : KeyValueStore<KeyValueStoreDBreeze.KeyValueStoreDBreeze>, IChainRepositoryStore
    {
        public ChainRepositoryStore(Network network, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.BlockPath, loggerFactory, dateTimeProvider, new DBreezeSerializer(network.Consensus.ConsensusFactory))
        {
        }
    }
}
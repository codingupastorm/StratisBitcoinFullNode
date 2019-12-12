using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockKeyValueStore : IKeyValueStore
    {
    }

    public class BlockKeyValueStore : KeyValueStore<KeyValueStoreLevelDB.KeyValueStoreLevelDB>, IBlockKeyValueStore
    {
        public BlockKeyValueStore(Network network, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            :base(dataFolder.BlockPath, loggerFactory, dateTimeProvider, new DBreezeSerializer(network.Consensus.ConsensusFactory))
        {
        }
    }
}

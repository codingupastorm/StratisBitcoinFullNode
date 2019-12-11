using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.NodeStorage.Interfaces;
using Stratis.Features.NodeStorage.KeyValueStore;
using Stratis.Features.NodeStorage.KeyValueStoreLevelDB;

namespace Stratis.Bitcoin.Features.BlockStore
{
    public interface IBlockKeyValueStore : IKeyValueStore
    {
    }

    public class BlockKeyValueStore : KeyValueStore<KeyValueStoreLevelDB>, IBlockKeyValueStore
    {
        public BlockKeyValueStore(Network network, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            :base(dataFolder.BlockPath, loggerFactory, dateTimeProvider, new DBreezeSerializer(network.Consensus.ConsensusFactory))
        {
        }
    }
}

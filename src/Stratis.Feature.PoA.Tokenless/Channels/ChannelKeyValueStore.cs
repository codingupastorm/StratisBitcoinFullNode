using Microsoft.Extensions.Logging;
using Stratis.Core.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStoreLevelDB;
using Stratis.Core.Utilities;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public interface IChannelKeyValueStore : IKeyValueStoreRepository
    {
    }

    public class ChannelKeyValueStore : KeyValueStoreLevelDB, IChannelKeyValueStore
    {
        public ChannelKeyValueStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory)
            : base(dataFolder.ChannelsPath, loggerFactory, repositorySerializer)
        {
        }
    }
}
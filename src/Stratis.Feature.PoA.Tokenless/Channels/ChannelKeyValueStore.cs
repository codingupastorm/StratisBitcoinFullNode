using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStoreLevelDB;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public interface IChannelKeyValueStore : IKeyValueStoreRepository
    {
    }

    public class ChannelKeyValueStore : KeyValueStoreLevelDB, IChannelKeyValueStore
    {
        public ChannelKeyValueStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.ChannelsPath, loggerFactory, repositorySerializer)
        {
        }
    }
}
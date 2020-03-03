using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public interface IPollsKeyValueStore : IKeyValueStore
    {
    }

    public class PollsKeyValueStore : KeyValueStoreLevelDB.KeyValueStoreLevelDB, IPollsKeyValueStore
    {
        public PollsKeyValueStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.PollsPath, loggerFactory, repositorySerializer)
        {
        }
    }
}

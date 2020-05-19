using Microsoft.Extensions.Logging;
using Stratis.Core.Configuration;
using Stratis.Core.Interfaces;
using Stratis.Core.NodeStorage.KeyValueStoreLevelDB;
using Stratis.Core.Utilities;

namespace Stratis.Features.PoA.Voting
{
    public interface IPollsKeyValueStore : IKeyValueStore
    {
    }

    public class PollsKeyValueStore : KeyValueStoreLevelDB, IPollsKeyValueStore
    {
        public PollsKeyValueStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.PollsPath, loggerFactory, repositorySerializer)
        {
        }
    }
}

using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.KeyValueStoreLevelDB;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Core.Store
{
    public interface IPrivateDataKeyValueStore : IKeyValueRepositoryStore
    {

    }
    public class PrivateDataKeyValueStore : KeyValueStoreLevelDB, IPrivateDataKeyValueStore
    {
        public PrivateDataKeyValueStore(string rootPath, ILoggerFactory loggerFactory, IRepositorySerializer repositorySerializer) : base(rootPath, loggerFactory, repositorySerializer)
        {
        }
    }
}
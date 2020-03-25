using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.KeyValueStoreLevelDB;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Core.Store
{
    public interface IPrivateDataKeyValueStore : IKeyValueRepositoryStore
    {
    }

    public interface IPrivateDataStore
    {
        void StoreBytes(uint160 contractAddress, byte[] key, byte[] value);
        void GetBytes(uint160 contractAddress, byte[] key);
    }

    public class PrivateDataStore : IPrivateDataStore
    {
        public PrivateDataStore(IPrivateDataKeyValueStore privateDataKeyValueStore)
        {
            throw new System.NotImplementedException();
        }

        public void StoreBytes(uint160 contractAddress, byte[] key, byte[] value)
        {
            throw new System.NotImplementedException();
        }

        public void GetBytes(uint160 contractAddress, byte[] key)
        {
            throw new System.NotImplementedException();
        }
    }

    public class PrivateDataKeyValueStore : KeyValueStoreLevelDB, IPrivateDataKeyValueStore
    {
        public PrivateDataKeyValueStore(string rootPath, ILoggerFactory loggerFactory, IRepositorySerializer repositorySerializer) : base(rootPath, loggerFactory, repositorySerializer)
        {
        }
    }
}
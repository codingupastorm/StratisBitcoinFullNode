using System.IO;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStoreLevelDB;
using Stratis.Core.Utilities;

namespace Stratis.SmartContracts.Core.Receipts
{
    public interface IReceiptKVStore : IKeyValueStore
    {
    }

    public class PersistentReceiptKVStore : KeyValueStoreLevelDB, IReceiptKVStore
    {
        public PersistentReceiptKVStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(Path.Combine(dataFolder.SmartContractStatePath, PersistentReceiptRepository.TableName), loggerFactory, repositorySerializer)
        {
        }
    }
}
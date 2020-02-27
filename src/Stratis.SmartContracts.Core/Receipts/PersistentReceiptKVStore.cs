using System.IO;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;
using Stratis.Bitcoin.KeyValueStoreLevelDB;

namespace Stratis.SmartContracts.Core.Receipts
{
    public interface IReceiptKVStore : IKeyValueStore
    {
    }

    public class PersistentReceiptKVStore : KeyValueStoreLevelDB, IReceiptKVStore
    {
        public PersistentReceiptKVStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(loggerFactory, repositorySerializer)
        {
            this.Init(Path.Combine(dataFolder.SmartContractStatePath, PersistentReceiptRepository.TableName));
        }
    }
}
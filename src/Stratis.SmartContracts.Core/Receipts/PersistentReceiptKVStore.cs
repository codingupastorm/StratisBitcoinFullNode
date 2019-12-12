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

    public class PersistentReceiptKVStore : KeyValueStore<KeyValueStoreLevelDB>, IReceiptKVStore
    {
        public PersistentReceiptKVStore(Network network, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(Path.Combine(dataFolder.SmartContractStatePath, PersistentReceiptRepository.TableName), loggerFactory, dateTimeProvider, new DBreezeSerializer(network.Consensus.ConsensusFactory))
        {
        }
    }
}
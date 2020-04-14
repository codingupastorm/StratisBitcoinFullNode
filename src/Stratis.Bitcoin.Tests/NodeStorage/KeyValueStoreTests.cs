using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.NodeStorage
{
    public class TestKeyValueStore : KeyValueStoreLevelDB.KeyValueStoreLevelDB
    {
        public TestKeyValueStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.BlockPath, loggerFactory, repositorySerializer)
        {
        }
    }

    public class KeyValueStoreTests
    {
        public IKeyValueStoreRepository GetStore([System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "", Network network = null)
        {
            network = network ?? KnownNetworks.StratisMain;

            string dataDir = TestBase.CreateDataFolder(this, callingMethod, network).RootPath;
            var nodeSettings = new NodeSettings(networksSelector: Networks.Networks.Bitcoin, args: new[] { $"-datadir={dataDir}" });
            var serializer = new RepositorySerializer(network.Consensus.ConsensusFactory);

            return new TestKeyValueStore(serializer, nodeSettings.DataFolder, nodeSettings.LoggerFactory, DateTimeProvider.Default);
        }

        [Fact]
        public void CanCreateKeyValueStore()
        {
            IKeyValueStoreRepository store = GetStore();
        }

        [Fact]
        public void CanStoreAndRetrieveValue()
        {
            using (IKeyValueStoreRepository store = GetStore())
            {
                using (IKeyValueStoreTransaction transaction = store.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, "test"))
                {
                    int key = 0;
                    int value = 99;

                    transaction.Insert("test", key, value);

                    Assert.True(transaction.Select("test", key, out int value2));

                    Assert.Equal(value, value2);
                }
            }
        }

        [Fact]
        public void CanStoreAndRetrieveRange()
        {
            using (IKeyValueStoreRepository store = GetStore())
            {
                using (IKeyValueStoreTransaction transaction = store.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, "test"))
                {
                    int[] keys = { 0, 2, 4 };
                    int[] values = { 10, 12, 13 };

                    transaction.InsertMultiple("test", keys.Select((k, n) => (k, values[n])).ToArray());

                    // Test SelectForward and SelectBackward for the case where the first/last key actually exists.
                    var data1 = transaction.SelectForward<int, int>("test", 2).ToArray();

                    Assert.Equal(values[1], data1[0].Item2);
                    Assert.Equal(values[2], data1[1].Item2);

                    var data2 = transaction.SelectForward<int, int>("test", 2, includeFirstKey: false).ToArray();

                    Assert.Equal(values[2], data2[0].Item2);

                    var data3 = transaction.SelectBackward<int, int>("test", 2).ToArray();

                    Assert.Equal(values[1], data3[0].Item2);
                    Assert.Equal(values[0], data3[1].Item2);

                    var data4 = transaction.SelectBackward<int, int>("test", 2, includeLastKey: false).ToArray();

                    Assert.Equal(values[0], data4[0].Item2);

                    // Test SelectForward and SelectBackward for the case where the first/last key does not exist.
                    var data11 = transaction.SelectForward<int, int>("test", 1).ToArray();

                    Assert.Equal(values[1], data11[0].Item2);
                    Assert.Equal(values[2], data11[1].Item2);

                    var data12 = transaction.SelectForward<int, int>("test", 1, includeFirstKey: false).ToArray();

                    Assert.Equal(values[1], data12[0].Item2);
                    Assert.Equal(values[2], data12[1].Item2);

                    var data13 = transaction.SelectBackward<int, int>("test", 3).ToArray();

                    Assert.Equal(values[1], data13[0].Item2);
                    Assert.Equal(values[0], data13[1].Item2);

                    var data14 = transaction.SelectBackward<int, int>("test", 3, includeLastKey: false).ToArray();

                    Assert.Equal(values[1], data14[0].Item2);
                    Assert.Equal(values[0], data14[1].Item2);
                }
            }
        }
    }
}

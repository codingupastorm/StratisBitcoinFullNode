using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.PoA.ProtocolEncryption;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Feature.PoA.Tokenless.Wallet;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.Util;
using Xunit;

namespace Stratis.Feature.PoA.Tokenless.Tests
{
    public class TokenlessTransactionTests
    {
        private static readonly ReadWriteSetKey Key1 = new ReadWriteSetKey(uint160.One, Encoding.UTF8.GetBytes("key1"));
        private static readonly ReadWriteSetKey Key2 = new ReadWriteSetKey(uint160.One, Encoding.UTF8.GetBytes("key2"));
        private static readonly ReadWriteSetKey Key3 = new ReadWriteSetKey(uint160.One, Encoding.UTF8.GetBytes("key3"));
        private static readonly ReadWriteSetKey Key4 = new ReadWriteSetKey(uint160.One, Encoding.UTF8.GetBytes("key4"));
        private static readonly ReadWriteSetKey Key1DifferentReference = new ReadWriteSetKey(uint160.One, Encoding.UTF8.GetBytes("key1"));
        private const string Version1 = "1.1";
        private const string Version2 = "1.2";
        private static byte[] Value1 = new byte[] { 0, 1, 2, 3 };
        private static readonly byte[] Value2 = new byte[] { 4, 5, 6, 7 };

        public ReadWriteSetTransactionSerializer GetBuilder(Network network = null, [System.Runtime.CompilerServices.CallerMemberName] string callingMethod = "")
        {
            network = network ?? new TokenlessNetwork();

            string testDir = TestBase.GetTestDirectoryPath(this, callingMethod);
            var settings = new NodeSettings(network, args: new[] { $"datadir={testDir}", "password=test" });
            var revocationChecker = new RevocationChecker(settings, null, settings.LoggerFactory, DateTimeProvider.Default);
            var certificatesManager = new CertificatesManager(settings.DataFolder, settings, settings.LoggerFactory, revocationChecker, network);
            var tokenlessWalletManager = new TokenlessWalletManager(network, settings.DataFolder, new TokenlessWalletSettings(settings), certificatesManager, settings.LoggerFactory);
            tokenlessWalletManager.Initialize();
            var signer = new TokenlessSigner(network, new SenderRetriever());
            var endorsementSigner = new EndorsementSigner(network, signer, tokenlessWalletManager);
            var builder = new ReadWriteSetTransactionSerializer(network, endorsementSigner);

            return builder;
        }

        [Fact]
        public void CanBuildTransactionWithRWS()
        {
            // Build a ReadWriteSet.
            var rws = new ReadWriteSetBuilder();

            rws.AddReadItem(Key1, Version1);
            rws.AddReadItem(Key2, Version2);

            rws.AddWriteItem(Key3, Value1);
            rws.AddWriteItem(Key4, Value2);

            Assert.Equal(2, rws.ReadSet.Count);
            Assert.Equal(2, rws.WriteSet.Count);

            var readSet = rws.ReadSet.ToList();
            Assert.Equal(Key1, readSet[0].Key);
            Assert.Equal(Version1, readSet[0].Value);
            Assert.Equal(Key2, readSet[1].Key);
            Assert.Equal(Version2, readSet[1].Value);

            var writeSet = rws.WriteSet.ToList();
            Assert.Equal(Key3, writeSet[0].Key);
            Assert.Equal(Value1, writeSet[0].Value);
            Assert.Equal(Key4, writeSet[1].Key);
            Assert.Equal(Value2, writeSet[1].Value);

            // Record the ReadWriteSet.
            ReadWriteSet readWriteSet = rws.GetReadWriteSet();

            // Create a transaction containing the ReadWriteSet.
            ReadWriteSetTransactionSerializer builder = GetBuilder();
            Transaction tx = builder.Build(readWriteSet);

            // Recover the ReadWriteSet from the transaction.
            ReadWriteSet readWriteSet2 = builder.GetReadWriteSet(tx);
            Assert.NotNull(readWriteSet2);

            // Compare the original ReadWriteSet's json with the recovered ReadWriteSet's json.
            Assert.Equal(readWriteSet.ToJson(), readWriteSet2.ToJson());
        }
    }
}

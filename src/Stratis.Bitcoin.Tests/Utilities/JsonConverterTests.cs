using System;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.OpenAsset;
using Stratis.Core.Networks;
using Stratis.Core.Utilities.JsonConverters;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class JsonConverterTests
    {
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanSerializeInJson()
        {
            var network = new BitcoinMain();

            var k = new Key();
            CanSerializeInJsonCore(DateTimeOffset.UtcNow);
            CanSerializeInJsonCore(new byte[] { 1, 2, 3 });
            CanSerializeInJsonCore(k);
            CanSerializeInJsonCore(Money.Coins(5.0m));
            CanSerializeInJsonCore(k.PubKey.GetAddress(network));
            CanSerializeInJsonCore(new KeyPath("1/2"));
            CanSerializeInJsonCore(new uint256(RandomUtils.GetBytes(32)));
            CanSerializeInJsonCore(new uint160(RandomUtils.GetBytes(20)));
            CanSerializeInJsonCore(new AssetId(k.PubKey));
            CanSerializeInJsonCore(k.PubKey.ScriptPubKey);
            CanSerializeInJsonCore(new Key().PubKey.WitHash.GetAddress(network));
            CanSerializeInJsonCore(new Key().PubKey.WitHash.ScriptPubKey.GetWitScriptAddress(network));
            ECDSASignature sig = k.Sign(new uint256(RandomUtils.GetBytes(32)));
            CanSerializeInJsonCore(sig);
            CanSerializeInJsonCore(new TransactionSignature(sig, SigHash.All));
            CanSerializeInJsonCore(k.PubKey.Hash);
            CanSerializeInJsonCore(k.PubKey.ScriptPubKey.Hash);
            CanSerializeInJsonCore(k.PubKey.WitHash);
            CanSerializeInJsonCore(k);
            CanSerializeInJsonCore(k.PubKey);
            CanSerializeInJsonCore(new WitScript(new Script(Op.GetPushOp(sig.ToDER()), Op.GetPushOp(sig.ToDER()))));
            CanSerializeInJsonCore(new LockTime(1));
            CanSerializeInJsonCore(new LockTime(DateTime.UtcNow));
        }

        [Fact]
        public void CanSerializeRandomClass()
        {
            var network = new BitcoinRegTest();
            string str = Serializer.ToString(new DummyClass() { ExtPubKey = new ExtKey().Neuter().GetWif(network) }, network);
            Assert.NotNull(Serializer.ToObject<DummyClass>(str, network));
        }

        private T CanSerializeInJsonCore<T>(T value)
        {
            string str = Serializer.ToString(value);
            T obj2 = Serializer.ToObject<T>(str, new BitcoinMain());
            Assert.Equal(str, Serializer.ToString(obj2));
            return obj2;
        }
    }

    public class DummyClass
    {
        public BitcoinExtPubKey ExtPubKey
        {
            get; set;
        }
    }
}
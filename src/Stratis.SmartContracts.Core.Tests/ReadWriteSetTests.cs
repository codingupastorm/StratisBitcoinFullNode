using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.SmartContracts.Core.ReadWrite;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests
{
    public class ReadWriteSetTests
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

        [Fact]
        public void InsertSeparateValues()
        {
            var rws = new ReadWriteSet();

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
        }

        [Fact]
        public void MultipleWritesSameKey()
        {
            // Only the last written value is in the RWS.
            var rws = new ReadWriteSet();
            rws.AddWriteItem(Key1, Value1);
            rws.AddWriteItem(Key1DifferentReference, Value1);
            rws.AddWriteItem(Key1, Value2);
            Assert.Single(rws.WriteSet);
            Assert.Equal(Value2, rws.WriteSet.ToList()[0].Value);
        }

        [Fact]
        public void ReadAfterWriteNotIncluded()
        {
            var rws = new ReadWriteSet();

            rws.AddWriteItem(Key1, Value1);
            rws.AddReadItem(Key1, Version1);

            // Our write should be there.
            Assert.Single(rws.WriteSet);

            // But the read that occurred after it should not, as we were reading a value set during the execution anyhow.
            Assert.Empty(rws.ReadSet);
        }

        [Fact]
        public void ChangesToBytesDoesntAffectReadWriteSet()
        {
            var rws = new ReadWriteSet();

            byte[] value = new byte[]{0,1,2,3};

            rws.AddWriteItem(Key1, value);

            // If something messes with the bytes after they are set in the RWS
            value[0] = 68;

            // They should still have the correct value.
            Assert.Equal(0, rws.WriteSet.ToList()[0].Value[0]);
        }
    }
}

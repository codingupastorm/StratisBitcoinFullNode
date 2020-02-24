using System.Linq;
using Xunit;

namespace Stratis.SmartContracts.Core.Tests
{
    public class ReadWriteSetTests
    {
        private const string Key1 = "key1";
        private const string Key2 = "key2";
        private const string Key3 = "key3";
        private const string Key4 = "key4";
        private const string Version1 = "1.1";
        private const string Version2 = "1.2";
        private static readonly byte[] Value1 = new byte[] { 0, 1, 2, 3 };
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
    }
}

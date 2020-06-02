using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Stratis.Core.Networks;
using Xunit;

namespace NBitcoin.Tests
{
    public class NetworkTests
    {
        private readonly Network networkMain;

        public NetworkTests()
        {
            this.networkMain = new BitcoinMain();
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void ReadMagicByteWithFirstByteDuplicated()
        {
            List<byte> bytes = this.networkMain.MagicBytes.ToList();
            bytes.Insert(0, bytes.First());

            using (var memstrema = new MemoryStream(bytes.ToArray()))
            {
                bool found = this.networkMain.ReadMagic(memstrema, new CancellationToken());
                Assert.True(found);
            }
        }
    }
}
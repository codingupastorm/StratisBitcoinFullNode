using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NBitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace NBitcoin.Tests
{
    public class NetworkTests
    {
        private readonly Network networkMain;
        private readonly Network stratisMain;
        private readonly Network stratisTest;
        private readonly Network stratisRegTest;

        public NetworkTests()
        {
            this.networkMain = KnownNetworks.Main;
            this.stratisMain = KnownNetworks.StratisMain;
            this.stratisTest = KnownNetworks.StratisTest;
            this.stratisRegTest = KnownNetworks.StratisRegTest;
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanGetNetworkFromName()
        {
            Network bitcoinMain = KnownNetworks.Main;
            Network bitcoinTestnet = KnownNetworks.TestNet;
            Network bitcoinRegtest = KnownNetworks.RegTest;
            Assert.Equal(NetworkRegistration.GetNetwork("main"), bitcoinMain);
            Assert.Equal(NetworkRegistration.GetNetwork("mainnet"), bitcoinMain);
            Assert.Equal(NetworkRegistration.GetNetwork("MainNet"), bitcoinMain);
            Assert.Equal(NetworkRegistration.GetNetwork("test"), bitcoinTestnet);
            Assert.Equal(NetworkRegistration.GetNetwork("testnet"), bitcoinTestnet);
            Assert.Equal(NetworkRegistration.GetNetwork("regtest"), bitcoinRegtest);
            Assert.Equal(NetworkRegistration.GetNetwork("reg"), bitcoinRegtest);
            Assert.Equal(NetworkRegistration.GetNetwork("stratismain"), this.stratisMain);
            Assert.Equal(NetworkRegistration.GetNetwork("StratisMain"), this.stratisMain);
            Assert.Equal(NetworkRegistration.GetNetwork("StratisTest"), this.stratisTest);
            Assert.Equal(NetworkRegistration.GetNetwork("stratistest"), this.stratisTest);
            Assert.Equal(NetworkRegistration.GetNetwork("StratisRegTest"), this.stratisRegTest);
            Assert.Equal(NetworkRegistration.GetNetwork("stratisregtest"), this.stratisRegTest);
            Assert.Null(NetworkRegistration.GetNetwork("invalid"));
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void RegisterNetworkTwiceReturnsSameNetwork()
        {
            Network main = KnownNetworks.Main;
            Network reregistered = NetworkRegistration.Register(main);
            Assert.Equal(main, reregistered);
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

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void MineGenesisBlockWithMissingParametersThrowsException()
        {
            Assert.Throws<ArgumentException>(() => Network.MineGenesisBlock(null, "some string", new Target(new uint256()), Money.Zero));
            Assert.Throws<ArgumentException>(() => Network.MineGenesisBlock(new ConsensusFactory(), "", new Target(new uint256()), Money.Zero));
            Assert.Throws<ArgumentException>(() => Network.MineGenesisBlock(new ConsensusFactory(), "some string", null, Money.Zero));
            Assert.Throws<ArgumentException>(() => Network.MineGenesisBlock(new ConsensusFactory(), "some string", new Target(new uint256()), null));
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void MineGenesisBlockWithLongCoinbaseTextThrowsException()
        {
            string coinbaseText100Long = "1111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111";
            Assert.Throws<ArgumentException>(() => Network.MineGenesisBlock(new ConsensusFactory(), coinbaseText100Long, new Target(new uint256()), Money.Zero));
        }
    }
}
using System.IO;
using NBitcoin.DataEncoders;
using Stratis.Core.Networks;
using Xunit;

namespace NBitcoin.Tests
{
    public class Checkblock_Tests
    {
        private readonly Network networkMain;

        public Checkblock_Tests()
        {
            this.networkMain = new BitcoinMain();
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanCalculateMerkleRoot()
        {
            Block block = this.networkMain.CreateBlock();
            block.ReadWrite(Encoders.Hex.DecodeData(File.ReadAllText(TestDataLocations.GetFileFromDataFolder("block169482.txt"))), this.networkMain.Consensus.ConsensusFactory);
            Assert.Equal(block.Header.HashMerkleRoot, block.GetMerkleRoot().Hash);
        }
    }
}
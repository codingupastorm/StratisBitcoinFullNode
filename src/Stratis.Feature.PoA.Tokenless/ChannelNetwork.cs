using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless
{
    /// <summary>
    /// Serializable version of the <see cref="Network"/> class.
    /// </summary>
    public sealed class ChannelNetwork : Network
    {
        public ChannelNetwork(Block genesisBlock)
        {
            this.Genesis = genesisBlock;
        }
    }
}

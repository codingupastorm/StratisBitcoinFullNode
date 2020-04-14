using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless.Channels.Requests
{
    public sealed class ChannelDefinition : IBitcoinSerializable
    {
        private string name;

        /// <summary> The name of the channel to create.</summary>
        public string Name { get { return this.name; } set { this.name = value; } }

        public void ReadWrite(BitcoinStream s)
        {
            s.ReadWrite(ref this.name);
        }
    }
}
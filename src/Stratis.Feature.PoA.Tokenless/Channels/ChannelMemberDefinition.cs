using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless.Channels.Requests
{
    public sealed class ChannelMemberDefinition : IBitcoinSerializable
    {
        private string channelName;
        private string memberPublicKey;

        /// <summary> The id of the channel to create.</summary>
        public string ChannelName { get { return this.channelName; } set { this.channelName = value; } }

        /// <summary> The name of the channel to create.</summary>
        public string MemberPublicKey { get { return this.memberPublicKey; } set { this.memberPublicKey = value; } }

        public void ReadWrite(BitcoinStream s)
        {
            s.ReadWrite(ref this.channelName);
            s.ReadWrite(ref this.memberPublicKey);
        }
    }
}
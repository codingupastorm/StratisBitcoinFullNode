using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    public sealed class ChannelMemberDefinition : IBitcoinSerializable
    {
        private string channelName;
        private string memberPublicKey;

        /// <summary> The name of the channel to create.</summary>
        public string ChannelName { get { return this.channelName; } set { this.channelName = value; } }

        /// <summary> The public key of the member to add.</summary>
        public string MemberPublicKey { get { return this.memberPublicKey; } set { this.memberPublicKey = value; } }

        public void ReadWrite(BitcoinStream s)
        {
            s.ReadWrite(ref this.channelName);
            s.ReadWrite(ref this.memberPublicKey);
        }
    }
}
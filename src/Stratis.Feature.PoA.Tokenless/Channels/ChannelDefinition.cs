using NBitcoin;
using Stratis.SmartContracts.Core.AccessControl;

namespace Stratis.Feature.PoA.Tokenless.Channels
{
    /// <summary> A persistable class that stores a serialized version of the channel network.</summary>
    public sealed class ChannelDefinition : IBitcoinSerializable
    {
        private int id;
        private string name;
        private string networkJson;

        /// <summary> The name of the channel.</summary>
        public string Name { get { return this.name; } set { this.name = value; } }

        /// <summary> The id of the channel.</summary>
        public int Id { get { return this.id; } set { this.id = value; } }

        /// <summary>Who can access the channel. This will be kept up to date over time.</summary>
        public AccessControlList AccessList { get; set; }

        /// <summary> The serialized version of the channel network.</summary>
        public string NetworkJson { get { return this.networkJson; } set { this.networkJson = value; } }

        /// <inheritdoc/>
        public void ReadWrite(BitcoinStream s)
        {
            s.ReadWrite(ref this.id);
            s.ReadWrite(ref this.name);
            s.ReadWrite(ref this.networkJson);

            if (s.Serializing)
            {
                string accessListJson = this.AccessList.ToJson();
                s.ReadWrite(ref accessListJson);
            }
            else
            {
                string accessListJson = null;
                s.ReadWrite(ref accessListJson);
                this.AccessList = AccessControlList.FromJson(accessListJson);
            }
        }
    }
}
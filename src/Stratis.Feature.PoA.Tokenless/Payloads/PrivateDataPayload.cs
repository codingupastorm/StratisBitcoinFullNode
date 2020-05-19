using NBitcoin;
using Stratis.Core.P2P.Protocol.Payloads;

namespace Stratis.Feature.PoA.Tokenless.Payloads
{
    [Payload("privdata")]
    public class PrivateDataPayload : Payload
    {
        private uint256 id;

        private uint blockHeight;

        private byte[] readWriteSetData;

        public uint256 Id => this.id;

        public uint BlockHeight => this.blockHeight;

        public byte[] ReadWriteSetData => this.readWriteSetData;

        /// <remarks>Needed for deserialization.</remarks>
        public PrivateDataPayload()
        {
        }

        public PrivateDataPayload(uint256 id, uint blockHeight, byte[] readWriteSetData)
        {
            this.id = id;
            this.blockHeight = blockHeight;
            this.readWriteSetData = readWriteSetData;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.id);
            stream.ReadWrite(ref this.blockHeight);
            if (stream.Serializing)
            {
                stream.ReadWrite(ref this.readWriteSetData);
            }
            else
            {
                byte[] data = new byte[stream.Inner.Length - stream.Inner.Position];
                stream.ReadWrite(ref data);
                this.readWriteSetData = data;
            }
        }

        public override string ToString()
        {
            return $"{nameof(this.Command)}:'{this.Command}',{nameof(this.Id)}:'{this.id}'";
        }
    }
}

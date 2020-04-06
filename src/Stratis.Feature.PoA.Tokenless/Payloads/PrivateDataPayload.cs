using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Feature.PoA.Tokenless.Payloads
{
    public class PrivateDataPayload : Payload
    {
        private uint256 transactionId;

        private uint blockHeight;

        private byte[] readWriteSetData;

        public uint256 TransactionId => this.transactionId;

        public uint BlockHeight => this.blockHeight;

        public byte[] ReadWriteSetData => this.readWriteSetData;

        /// <remarks>Needed for deserialization.</remarks>
        public PrivateDataPayload()
        {
        }

        public PrivateDataPayload(uint256 transactionId, uint blockHeight, byte[] readWriteSetData)
        {
            this.transactionId = transactionId;
            this.blockHeight = blockHeight;
            this.readWriteSetData = readWriteSetData;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.transactionId);
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
            return $"{nameof(this.Command)}:'{this.Command}',{nameof(this.TransactionId)}:'{this.transactionId}'";
        }
    }
}

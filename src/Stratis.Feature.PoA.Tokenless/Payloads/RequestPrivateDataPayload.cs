using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Feature.PoA.Tokenless.Payloads
{
    [Payload("privreq")]
    public class RequestPrivateDataPayload : Payload
    {
        private uint256 txId;

        public uint256 TxId => this.txId;

        /// <remarks>Needed for deserialization.</remarks>
        public RequestPrivateDataPayload()
        {
        }

        public RequestPrivateDataPayload(uint256 txId)
        {
            this.txId = txId;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.txId);
        }

        public override string ToString()
        {
            return $"{nameof(this.Command)}:'{this.Command}',{nameof(this.TxId)}:'{this.txId}'";
        }
    }
}

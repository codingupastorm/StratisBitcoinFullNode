using NBitcoin;
using Stratis.Core.P2P.Protocol.Payloads;

namespace Stratis.Feature.PoA.Tokenless.Payloads
{
    [Payload("privreq")]
    public class RequestPrivateDataPayload : Payload
    {
        private uint256 id;

        public uint256 Id => this.id;

        /// <remarks>Needed for deserialization.</remarks>
        public RequestPrivateDataPayload()
        {
        }

        public RequestPrivateDataPayload(uint256 id)
        {
            this.id = id;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.id);
        }

        public override string ToString()
        {
            return $"{nameof(this.Command)}:'{this.Command}',{nameof(this.Id)}:'{this.id}'";
        }
    }
}

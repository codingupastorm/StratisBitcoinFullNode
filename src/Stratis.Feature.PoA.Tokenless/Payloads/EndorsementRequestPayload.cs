using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Feature.PoA.Tokenless.Payloads
{
    [Payload("endorsement-request")]
    public class EndorsementRequestPayload : Payload
    {
        private Transaction transaction;

        public Transaction Transaction => this.transaction;

        /// <remarks>Needed for deserialization.</remarks>
        public EndorsementRequestPayload()
        {
        }

        public EndorsementRequestPayload(Transaction transaction)
        {
            this.transaction = transaction;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.transaction);
        }

        public override string ToString()
        {
            return $"{nameof(this.Command)}:'{this.Command}',{nameof(this.Transaction)}:'{this.transaction.GetHash()}'";
        }
    }
}

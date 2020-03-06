using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Feature.PoA.Tokenless.Payloads
{
    [Payload("endorsement")]
    public class EndorsementPayload : Payload
    {
        private Transaction transaction;

        public Transaction Transaction => this.transaction;

        /// <remarks>Needed for deserialization.</remarks>
        public EndorsementPayload()
        {
        }

        public EndorsementPayload(Transaction transaction)
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

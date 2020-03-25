using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Feature.PoA.Tokenless.Payloads
{
    [Payload("proposal")]
    public class ProposalPayload : Payload
    {
        private Transaction transaction;

        public Transaction Transaction => this.transaction;

        private byte[] transientData;

        public byte[] TransientData => this.transientData;

        /// <remarks>Needed for deserialization.</remarks>
        public ProposalPayload()
        {
        }

        public ProposalPayload(Transaction transaction, byte[] transientData)
        {
            this.transaction = transaction;
            this.transientData = transientData;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.transaction);
            stream.ReadWrite(ref this.transientData);
        }

        public override string ToString()
        {
            return $"{nameof(this.Command)}:'{this.Command}',{nameof(this.Transaction)}:'{this.transaction.GetHash()}'";
        }
    }
}

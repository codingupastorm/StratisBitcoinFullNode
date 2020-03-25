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

            // Needlessly complicated code that will only serialize and deserialize the transientData if it exists.
            if (stream.Serializing)
            {
                if (this.transientData != null)
                {
                    stream.ReadWrite(ref this.transientData);
                }
            }
            else
            {
                if (stream.Inner.Position < stream.Inner.Length)
                {
                    byte[] data = new byte[stream.Inner.Length - stream.Inner.Position];
                    stream.ReadWrite(ref this.transientData);
                }
            }
        }

        public override string ToString()
        {
            return $"{nameof(this.Command)}:'{this.Command}',{nameof(this.Transaction)}:'{this.transaction.GetHash()}'";
        }
    }
}

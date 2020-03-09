using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Feature.PoA.Tokenless.Payloads
{
    [Payload("endorsement")]
    public class EndorsementPayload : Payload
    {
        private uint256 proposalId;
        private Transaction transaction;

        public uint256 ProposalId => this.proposalId;
        public Transaction Transaction => this.transaction;

        /// <remarks>Needed for deserialization.</remarks>
        public EndorsementPayload()
        {
        }

        public EndorsementPayload(Transaction transaction, uint256 proposalId)
        {
            this.proposalId = proposalId;
            this.transaction = transaction;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.proposalId);
            stream.ReadWrite(ref this.transaction);
        }

        public override string ToString()
        {
            return $"{nameof(this.Command)}:'{this.Command}',{nameof(this.ProposalId)}:'{this.proposalId}',{nameof(this.Transaction)}:'{this.transaction.GetHash()}'";
        }
    }
}

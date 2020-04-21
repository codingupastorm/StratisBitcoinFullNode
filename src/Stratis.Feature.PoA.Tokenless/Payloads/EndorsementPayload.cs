using NBitcoin;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Feature.PoA.Tokenless.Endorsement;

namespace Stratis.Feature.PoA.Tokenless.Payloads
{
    [Payload("endorsement")]
    public class EndorsementPayload : Payload
    {
        private uint256 proposalId;
        private SignedProposalResponse proposalResponse;

        public uint256 ProposalId => this.proposalId;
        public SignedProposalResponse ProposalResponse => this.proposalResponse;

        /// <remarks>Needed for deserialization.</remarks>
        public EndorsementPayload()
        {
        }

        public EndorsementPayload(SignedProposalResponse proposalResponse, uint256 proposalId)
        {
            this.proposalId = proposalId;
            this.proposalResponse = proposalResponse;
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.proposalId);
            stream.ReadWrite(ref this.proposalResponse);
        }

        public override string ToString()
        {
            return $"{nameof(this.Command)}:'{this.Command}',{nameof(this.ProposalId)}:'{this.proposalId}',{nameof(this.ProposalResponse)}:'{this.ProposalResponse.ProposalResponse.GetHash()}'";
        }
    }
}

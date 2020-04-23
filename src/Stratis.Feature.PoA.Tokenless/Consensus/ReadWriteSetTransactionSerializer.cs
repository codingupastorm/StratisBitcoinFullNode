using System.Text;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    public interface IReadWriteSetTransactionSerializer 
    {
        SignedProposalResponse Build(ReadWriteSet readWriteSet, ReadWriteSet privateReadWriteSet, uint256 proposalId);
        ReadWriteSet GetReadWriteSet(Transaction tx);
    }

    public class ReadWriteSetTransactionSerializer : IReadWriteSetTransactionSerializer
    {
        private readonly Network network;
        private readonly IEndorsementSigner endorsementSigner;

        public ReadWriteSetTransactionSerializer(Network network, IEndorsementSigner endorsementSigner)
        {
            this.network = network;
            this.endorsementSigner = endorsementSigner;
        }

        public SignedProposalResponse Build(ReadWriteSet readWriteSet, ReadWriteSet privateReadWriteSet,
            uint256 proposalId)
        {
            var proposalResponse = new ProposalResponse
            {
                ReadWriteSet = readWriteSet,
                ProposalId = proposalId
            };

            var endorsement = this.endorsementSigner.Sign(proposalResponse);

            var signedProposalResponse = new SignedProposalResponse
            {
                ProposalResponse = proposalResponse,
                Endorsement = endorsement,
                PrivateReadWriteSet = privateReadWriteSet
            };

            return signedProposalResponse;
        }

        public ReadWriteSet GetReadWriteSet(Transaction tx)
        {
            if (tx.Outputs.Count < 1)
                return null;

            var rwsData = TxReadWriteDataTemplate.Instance.ExtractScriptPubKeyParameters(tx.Outputs[0].ScriptPubKey);
            if (rwsData == null || rwsData.Length != 1)
                return null;

            return ReadWriteSet.FromJsonEncodedBytes(rwsData[0]);
        }
    }
}

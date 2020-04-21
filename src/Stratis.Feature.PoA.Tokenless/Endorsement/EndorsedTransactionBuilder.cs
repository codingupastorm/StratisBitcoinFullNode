using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    /// <summary>
    /// Builds a transaction that has satisfied an endorsement policy.
    /// </summary>
    public class EndorsedTransactionBuilder
    {
        private readonly IEndorsementSigner endorsementSigner;

        public EndorsedTransactionBuilder(IEndorsementSigner endorsementSigner)
        {
            this.endorsementSigner = endorsementSigner;
        }

        public Transaction Build(List<SignedProposalResponse> proposalResponses)
        {
            if (!ValidateProposalResponses(proposalResponses))
                return null;

            var transaction = new Transaction();
            
            // Endorsement signer uses the same logic to sign the first input with the transaction signing key.
            this.endorsementSigner.Sign(transaction);

            AddReadWriteSet(transaction, proposalResponses);
            AddEndorsements(transaction, proposalResponses.Select(p => p.Endorsement));

            return transaction;
        }

        private void AddEndorsements(Transaction transaction, IEnumerable<Endorsement> endorsements)
        {
            foreach(Endorsement endorsement in endorsements)
            {
                transaction.Outputs.Add(new TxOut(Money.Zero, new Script(endorsement.ToJson())));
            }
        }

        private static void AddReadWriteSet(Transaction transaction, List<SignedProposalResponse> proposalResponses)
        {
            // We can pick any RWS here as they should all be the same
            ReadWriteSet rws = GetReadWriteSet(proposalResponses);

            Script rwsScriptPubKey = TxReadWriteDataTemplate.Instance.GenerateScriptPubKey(rws.ToJsonEncodedBytes());

            transaction.Outputs.Add(new TxOut(Money.Zero, rwsScriptPubKey));
        }

        private static bool ValidateProposalResponses(List<SignedProposalResponse> proposalResponses)
        {
            // Nothing to compare against.
            if (proposalResponses.Count < 2) return true;

            var serializedProposalResponses = proposalResponses
                .Select(r => r.ProposalResponse.ToBytes())
                .ToList();

            // All elements should be the same.
            return serializedProposalResponses.All(b => b.SequenceEqual(serializedProposalResponses[0]));
        }

        private static ReadWriteSet GetReadWriteSet(List<SignedProposalResponse> proposalResponses)
        {
            return proposalResponses.First().ProposalResponse.ReadWriteSet;
        }
    }
}

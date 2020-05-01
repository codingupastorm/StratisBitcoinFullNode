using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsedTransactionBuilder
    {
        Transaction Build(IReadOnlyList<SignedProposalResponse> proposalResponses);

        bool TryParseTransaction(Transaction transaction, out IEnumerable<Endorsement> endorsements,
            out ReadWriteSet rws);
    }

    /// <summary>
    /// Builds a transaction that has satisfied an endorsement policy.
    /// </summary>
    public class EndorsedTransactionBuilder : IEndorsedTransactionBuilder
    {
        private readonly IEndorsementSigner endorsementSigner;

        public EndorsedTransactionBuilder(IEndorsementSigner endorsementSigner)
        {
            this.endorsementSigner = endorsementSigner;
        }

        public Transaction Build(IReadOnlyList<SignedProposalResponse> proposalResponses)
        {
            if (!ValidateProposalResponses(proposalResponses))
                return null;

            var transaction = new Transaction();

            // TODO at the moment this is the full RWS. We should check that only the public RWS is signed and returned by the endorser.
            AddReadWriteSet(transaction, proposalResponses);
            AddEndorsements(transaction, proposalResponses.Select(p => p.Endorsement));

            // Endorsement signer uses the same logic to sign the first input with the transaction signing key.
            this.endorsementSigner.Sign(transaction);

            return transaction;
        }

        public bool TryParseTransaction(Transaction transaction, out IEnumerable<Endorsement> endorsements, out ReadWriteSet rws)
        {
            endorsements = null;
            rws = null;

            // First output is the rws
            if (transaction.Outputs.Count < 1)
                return false;

            var rwsBytes = transaction.Outputs[0].ScriptPubKey.ToBytes();

            // This could be empty depending on the endorsement policy.
            var endorsementsBytes = transaction
                .Outputs
                .Skip(1)
                .Where(s => s.ScriptPubKey != null)
                .Select(s => s.ScriptPubKey.ToBytes())
                .ToList();

            try
            {
                rws = ReadWriteSet.FromJsonEncodedBytes(rwsBytes);
                endorsements = endorsementsBytes.Select(Endorsement.FromBytes);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void AddEndorsements(Transaction transaction, IEnumerable<Endorsement> endorsements)
        {
            foreach(Endorsement endorsement in endorsements)
            {
                transaction.Outputs.Add(new TxOut(Money.Zero, new Script(endorsement.ToJson())));
            }
        }

        private static void AddReadWriteSet(Transaction transaction, IEnumerable<SignedProposalResponse> proposalResponses)
        {
            // We can pick any RWS here as they should all be the same
            ReadWriteSet rws = GetReadWriteSet(proposalResponses);

            Script rwsScriptPubKey = TxReadWriteDataTemplate.Instance.GenerateScriptPubKey(rws.ToJsonEncodedBytes());

            transaction.Outputs.Add(new TxOut(Money.Zero, rwsScriptPubKey));
        }

        private static bool ValidateProposalResponses(IReadOnlyList<SignedProposalResponse> proposalResponses)
        {
            // Nothing to compare against.
            if (proposalResponses.Count < 2) return true;

            var serializedProposalResponses = proposalResponses
                .Select(r => r.ProposalResponse.ToBytes())
                .ToList();

            // All elements should be the same.
            return serializedProposalResponses.All(b => b.SequenceEqual(serializedProposalResponses[0]));
        }

        private static ReadWriteSet GetReadWriteSet(IEnumerable<SignedProposalResponse> proposalResponses)
        {
            return proposalResponses.First().ProposalResponse.ReadWriteSet;
        }
    }
}

using System.Collections.Generic;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    /// <summary>
    /// The logic for the endorsement validation rules.
    /// </summary>
    public class EndorsedContractTransactionValidationRule
    {
        private readonly IEndorsedTransactionBuilder endorsedTransactionBuilder;
        private readonly IEndorsementSignatureValidator signatureValidator;
        private readonly IOrganisationLookup organisationLookup;

        public enum EndorsementValidationErrorType
        {
            None,
            InvalidCall,
            Malformed,
            SignaturesInvalid
        }

        public EndorsedContractTransactionValidationRule(IEndorsedTransactionBuilder endorsedTransactionBuilder, IEndorsementSignatureValidator signatureValidator, IOrganisationLookup organisationLookup)
        {
            this.endorsedTransactionBuilder = endorsedTransactionBuilder;
            this.signatureValidator = signatureValidator;
            this.organisationLookup = organisationLookup;
        }

        public (bool, EndorsementValidationErrorType) CheckTransaction(Transaction transaction)
        {
            if (transaction.Outputs.Count == 0)
                return (false, EndorsementValidationErrorType.InvalidCall);

            // Check that this is a call contract transaction
            // For now, create contract transactions don't need endorsement.
            if (!transaction.Outputs[0].ScriptPubKey.IsReadWriteSet())
            {
                return (false, EndorsementValidationErrorType.InvalidCall);
            }

            if (!this.endorsedTransactionBuilder.TryParseTransaction(transaction, out IEnumerable<Endorsement.Endorsement> endorsements, out ReadWriteSet _))
            {
                return (false, EndorsementValidationErrorType.Malformed);
            }

            // Save a serialization roundtrip by getting the RWS bytes.
            var rwsBytes = transaction.Outputs[0].ScriptPubKey.ToBytes();

            if (this.signatureValidator.Validate(endorsements, rwsBytes))
            {
                return (true, EndorsementValidationErrorType.None);
            }

            return (false, EndorsementValidationErrorType.SignaturesInvalid);
        }
    }
}
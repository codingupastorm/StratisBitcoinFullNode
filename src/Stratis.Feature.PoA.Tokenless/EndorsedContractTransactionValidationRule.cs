using System.Collections.Generic;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless
{
    /// <summary>
    /// The logic for the endorsement validation rules.
    /// </summary>
    public class EndorsedContractTransactionValidationRule
    {
        public static Dictionary<EndorsementValidationErrorType, string> ErrorMessages = new Dictionary<EndorsementValidationErrorType, string>
        {
            { EndorsementValidationErrorType.None, "" },
            { EndorsementValidationErrorType.InvalidCall, "contract-transaction-invalid-call" },
            { EndorsementValidationErrorType.Malformed, "contract-transaction-endorsements-malformed" },
            { EndorsementValidationErrorType.PolicyInvalid, "contract-transaction-endorsement-policy-not-satisfied" },
            { EndorsementValidationErrorType.SignaturesInvalid, "contract-transaction-endorsement-signatures-invalid" }
        };

        private readonly IEndorsedTransactionBuilder endorsedTransactionBuilder;
        private readonly IEndorsementSignatureValidator signatureValidator;
        private readonly IEndorsementPolicyValidator policyValidator;

        public enum EndorsementValidationErrorType
        {
            None,
            InvalidCall,
            Malformed,
            PolicyInvalid,
            SignaturesInvalid
        }

        public EndorsedContractTransactionValidationRule(IEndorsedTransactionBuilder endorsedTransactionBuilder, IEndorsementSignatureValidator signatureValidator, IEndorsementPolicyValidator policyValidator)
        {
            this.endorsedTransactionBuilder = endorsedTransactionBuilder;
            this.signatureValidator = signatureValidator;
            this.policyValidator = policyValidator;
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

            if (!this.endorsedTransactionBuilder.TryParseTransaction(transaction, out IEnumerable<Endorsement.Endorsement> endorsements, out ReadWriteSet rws))
            {
                return (false, EndorsementValidationErrorType.Malformed);
            }

            if (!this.policyValidator.Validate(rws, endorsements))
            {
                var rwsBytes = EndorsedTransactionBuilder.ExtractRwsBytes(transaction);

                // Determine if we failed due to a signature problem.
                if (!this.signatureValidator.Validate(endorsements, rwsBytes))
                {
                    return (false, EndorsementValidationErrorType.SignaturesInvalid);
                }

                return (false, EndorsementValidationErrorType.PolicyInvalid);
            }

            return (true, EndorsementValidationErrorType.None);
        }
    }
}
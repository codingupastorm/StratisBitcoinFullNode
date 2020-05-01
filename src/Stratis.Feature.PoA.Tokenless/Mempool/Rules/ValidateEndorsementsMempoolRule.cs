﻿using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Endorsement;
using Stratis.Features.MemoryPool;
using Stratis.Features.MemoryPool.Interfaces;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless.Mempool.Rules
{
    public class ValidateEndorsementsMempoolRule : MempoolRule
    {
        private readonly IEndorsementValidator endorsementValidator;
        private readonly IEndorsedTransactionBuilder endorsedTransactionBuilder;

        public ValidateEndorsementsMempoolRule(IEndorsementValidator endorsementValidator, IEndorsedTransactionBuilder endorsedTransactionBuilder, Network network, ITxMempool mempool, MempoolSettings settings, ChainIndexer chainIndexer, ILoggerFactory loggerFactory) 
            : base(network, mempool, settings, chainIndexer, loggerFactory)
        {
            this.endorsementValidator = endorsementValidator;
            this.endorsedTransactionBuilder = endorsedTransactionBuilder;
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            // Check that this is a call contract transaction
            // For now, create contract transactions don't need endorsement.
            if (!context.Transaction.IsSmartContractExecTransaction())
            {
                this.logger.LogDebug($"{context.Transaction.GetHash()}' does not contain a contract call.");
                return;
            }

            var transaction = context.Transaction;

            if (!this.endorsedTransactionBuilder.TryParseTransaction(transaction, out IEnumerable<Endorsement.Endorsement> endorsements, out ReadWriteSet rws))
            {
                var errorMessage = $"Transaction '{transaction.GetHash()}' contained a contract transaction but one or more of its endorsements were malformed";

                context.State.Fail(new MempoolError(MempoolErrors.RejectMalformed, "contract-transaction-endorsements-malformed"), errorMessage).Throw();
                return;
            }

            // Save a serialization roundtrip by getting the RWS bytes.
            var rwsBytes = context.Transaction.Outputs[0].ScriptPubKey.ToBytes();

            if (!this.endorsementValidator.Validate(endorsements, rwsBytes))
            {
                var errorMessage = $"Transaction '{transaction.GetHash()}' contained a contract transaction but one or more of its endorsements contained invalid signatures";

                context.State.Fail(new MempoolError(MempoolErrors.RejectMalformed, "contract-transaction-endorsement-signatures-invalid"), errorMessage).Throw();
            }
        }
    }

    /// <summary>
    /// Shared logic for checking endorsed transactions in mempool and consensus rules.
    /// </summary>
    public static class EndorsedTransactionChecker
    {
        public static bool TryGetEndorsements(Transaction transaction, out List<Endorsement.Endorsement> endorsement)
        {

            var endorsementBytes = txOut.ScriptPubKey.ToBytes();

            try
            {
                endorsement = Endorsement.Endorsement.FromBytes(endorsementBytes);
                return true;
            }
            catch
            {
                endorsement = null;
                return false;
            }
        }
        public static void CheckEndorsement(IEndorsementValidator validator, TxOut txOut)
        {

            //validator.Validate()
        }
    }
}
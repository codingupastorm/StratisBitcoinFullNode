﻿using NBitcoin;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    public interface ITokenlessSigner
    {
        /// <summary>
        /// Inserts a TxIn to the given transaction that allows us to identify the sender
        /// and check a signature.
        /// </summary>
        void InsertSignedTxIn(Transaction transaction, ISecret key);

        /// <summary>
        /// Get the 20-byte address of the sender of this tokenless transaction.
        /// </summary>
        GetSenderResult GetSender(Transaction transaction);

        /// <summary>
        /// Tries to get the sender and if successful, tries to verify the signature.
        /// </summary>
        GetSenderResult GetSenderAndVerify(Transaction transaction);

        /// <summary>
        /// Verifies the transaction is signed correctly according to the Tokenless rules.
        /// </summary>
        bool Verify(Transaction transaction);
    }
}

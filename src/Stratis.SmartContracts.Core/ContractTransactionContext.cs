﻿using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Core
{
    public class ContractTransactionContext : IContractTransactionContext
    {
        private readonly ulong blockHeight;

        private readonly ulong txIndex;

        private readonly uint160 coinbaseAddress;

        private readonly Transaction transaction;

        private readonly TxOut contractTxOut;

        private readonly uint160 sender;

        /// <inheritdoc />
        public uint256 TransactionHash
        {
            get { return this.transaction.GetHash(); }
        }

        /// <inheritdoc />
        public uint160 Sender
        {
            get { return this.sender; }
        }

        /// <inheritdoc />
        public ulong TxOutValue
        {
            get { return this.contractTxOut.Value; }
        }

        /// <inheritdoc />
        public uint Nvout
        {
            get { return (uint) this.transaction.Outputs.IndexOf(this.contractTxOut); }
        }

        /// <inheritdoc />
        public byte[] Data
        {
            get { return this.contractTxOut.ScriptPubKey.ToBytes(); }
        }

        /// <inheritdoc />
        public uint160 CoinbaseAddress
        {
            get { return this.coinbaseAddress; }
        }

        /// <inheritdoc />
        public ulong BlockHeight
        {
            get { return this.blockHeight; }
        }

        /// <inheritdoc />
        public ulong TxIndex
        {
            get { return this.txIndex; }
        }

        public uint Time
        {
            get
            {
                return this.transaction.Time;
            }
        }

        public ContractTransactionContext(
            ulong blockHeight,
            ulong txIndex,
            uint160 coinbaseAddress,
            uint160 sender,
            Transaction transaction)
        {
            this.blockHeight = blockHeight;
            this.txIndex = txIndex;
            this.coinbaseAddress = coinbaseAddress;
            this.transaction = transaction;
            this.contractTxOut = transaction.Outputs.FirstOrDefault(x => x.ScriptPubKey.IsSmartContractExec());
            Guard.NotNull(this.contractTxOut, nameof(this.contractTxOut));

            this.sender = sender;
        }
    }
}

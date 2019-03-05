using NBitcoin;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public class Transfer : IBitcoinSerializable
    {
        public uint256 DepositTransactionId => this.depositTransactionId;
        private uint256 depositTransactionId;

        public string DepositTargetAddress => this.depositTargetAddress;
        private string depositTargetAddress;

        public long DepositAmount => this.depositAmount;
        private long depositAmount;

        public int DepositHeight => this.depositHeight;
        private int depositHeight;

        public uint DepositTime => this.depositTime;
        private uint depositTime;

        public Transaction PartialTransaction => this.partialTransaction;
        private Transaction partialTransaction;

        public uint256 BlockHash => this.blockHash;
        private uint256 blockHash;

        public int? BlockHeight => this.blockHeight;
        private int? blockHeight;

        public TransferStatus Status => this.status;
        private TransferStatus status;

        /// <summary>
        /// Parameter-less constructor for (de)serialization.
        /// </summary>
        public Transfer()
        {
        }

        /// <summary>
        /// Constructs this object from passed parameters.
        /// </summary>
        /// <param name="status">The status of the cross chain transfer transaction.</param>
        /// <param name="depositTransactionId">The transaction id of the deposit transaction.</param>
        /// <param name="depositTargetAddress">The target address of the deposit transaction.</param>
        /// <param name="depositAmount">The amount (in satoshis) of the deposit transaction.</param>
        /// <param name="depositHeight">The chain A height at which the deposit was made (if known).</param>
        /// <param name="partialTransaction">The unsigned partial transaction containing a full set of available UTXO's.</param>
        /// <param name="blockHash">The hash of the block where the transaction resides.</param>
        /// <param name="blockHeight">The height (in our chain) of the block where the transaction resides.</param>
        public Transfer(TransferStatus status, uint256 depositTransactionId, string depositTargetAddress, Money depositAmount,
            int depositHeight, uint depositTime, Transaction partialTransaction, uint256 blockHash = null, int? blockHeight = null)
        {
            this.status = status;
            this.depositTransactionId = depositTransactionId;
            this.depositTargetAddress = depositTargetAddress;
            this.depositAmount = depositAmount;
            this.depositHeight = depositHeight;
            this.depositTime = depositTime;
            this.partialTransaction = partialTransaction;
            this.blockHash = blockHash;
            this.blockHeight = blockHeight;
        }

        /// <summary>
        /// (De)serializes this object.
        /// </summary>
        /// <param name="stream">Stream to use for (de)serialization.</param>
        public void ReadWrite(BitcoinStream stream)
        {
            byte status = (byte)this.status;
            stream.ReadWrite(ref status);
            this.status = (TransferStatus)status;

            stream.ReadWrite(ref this.depositTransactionId);
            stream.ReadWrite(ref this.depositTargetAddress);
            stream.ReadWrite(ref this.depositAmount);
            stream.ReadWrite(ref this.depositHeight);
            stream.ReadWrite(ref this.depositTime);

            stream.ReadWrite(ref this.partialTransaction);

            if (!stream.Serializing && this.partialTransaction.Inputs.Count == 0 && this.partialTransaction.Outputs.Count == 0)
                this.partialTransaction = null;

            if (this.status == TransferStatus.SeenInBlock)
            {
                uint256 blockHash = this.blockHash ?? 0;
                stream.ReadWrite(ref blockHash);
                this.blockHash = (blockHash == 0) ? null : blockHash;

                int blockHeight = this.BlockHeight ?? -1;
                stream.ReadWrite(ref blockHeight);
                this.blockHeight = (blockHeight < 0) ? (int?)null : blockHeight;
            }
        }

        public static Transfer FromDeposit(IDeposit deposit, uint blockTime)
        {
            return new Transfer(
                TransferStatus.NotCreated,
                deposit.Id,
                deposit.TargetAddress,
                deposit.Amount,
                deposit.BlockNumber,
                blockTime,
                null
                );
        }
    }
}

﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using DBreeze;
using DBreeze.DataTypes;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Transaction = DBreeze.Transactions.Transaction;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    /// <summary>
    /// Holds information about all known deposits on the opposite chain.
    /// </summary>
    public class TransferRepository : ITransferRepository
    {
        /// <summary>This table contains the cross-chain transfer information.</summary>
        private const string DepositTableName = "deposits";

        /// <summary>The key of the counter-chain block tip that we have synced deposits to.</summary>
        private static readonly byte[] SyncedUpToBlockKey = new byte[] { 1 };

        private readonly DBreezeEngine db;
        private readonly DBreezeSerializer serializer;

        public TransferRepository(DataFolder dataFolder, IFederationGatewaySettings settings, DBreezeSerializer serializer)
        {
            string depositStoreName = "federatedTransfers" + settings.MultiSigAddress; // TODO: Unneccessary?
            string folder = Path.Combine(dataFolder.RootPath, depositStoreName);
            Directory.CreateDirectory(folder);
            this.db = new DBreezeEngine(folder);

            this.serializer = serializer;
        }

        /// <inheritdoc />
        public int GetSyncedBlockNumber()
        {
            using (Transaction dbreezeTransaction = this.db.GetTransaction())
            {
                return this.GetSyncedBlockNumberInternal(dbreezeTransaction);
            }
        }

        private int GetSyncedBlockNumberInternal(Transaction dbreezeTransaction)
        {
            Row<byte[], int> row = dbreezeTransaction.Select<byte[], int>(DepositTableName, SyncedUpToBlockKey);
            if (row.Exists)
                return row.Value;

            return -1;
        }

        private void PutSyncedBlockNumber(Transaction dbreezeTransaction, int syncedBlockNumber)
        {
            dbreezeTransaction.Insert<byte[], int>(DepositTableName, SyncedUpToBlockKey, syncedBlockNumber);
        }

        /// <inheritdoc />
        public bool SaveDeposits(IList<MaturedBlockDepositsModel> maturedBlockDeposits)
        {
            using (Transaction dbreezeTransaction = this.db.GetTransaction())
            {
                // Ensure that we're only adding new blocks worth of deposits.
                int syncedBlockNum = this.GetSyncedBlockNumberInternal(dbreezeTransaction);
                int nextSyncNum = syncedBlockNum + 1;
                maturedBlockDeposits = maturedBlockDeposits
                    .OrderBy(a => a.BlockInfo.BlockHeight)
                    .SkipWhile(m => m.BlockInfo.BlockHeight < nextSyncNum).ToArray();

                // Ensure we're not skipping any blocks
                if (maturedBlockDeposits.Count == 0 || maturedBlockDeposits.First().BlockInfo.BlockHeight != nextSyncNum)
                {
                    return false;
                }

                foreach (MaturedBlockDepositsModel maturedBlockDeposit in maturedBlockDeposits)
                {
                    // Ensure we're not skipping any blocks
                    if (maturedBlockDeposit.BlockInfo.BlockHeight != nextSyncNum)
                    {
                        return false;
                    }

                    foreach (IDeposit deposit in maturedBlockDeposit.Deposits)
                    {
                        // We should be able to trust input from the other node, but just in case it slips this invalid response in
                        if (maturedBlockDeposit.BlockInfo.BlockHeight != deposit.BlockNumber)
                        {
                            return false;
                        }

                        this.PutTransfer(dbreezeTransaction, Transfer.FromDeposit(deposit, maturedBlockDeposit.BlockInfo.BlockTime));
                    }

                    nextSyncNum++;
                }

                // Update db to where we are synced to
                this.PutSyncedBlockNumber(dbreezeTransaction, maturedBlockDeposits.Last().BlockInfo.BlockHeight);

                dbreezeTransaction.Commit();
            }

            return true;
        }

        private void PutTransfer(Transaction dbreezeTransaction, Transfer transfer)
        {
            Guard.NotNull(transfer, nameof(transfer));

            byte[] depositBytes = this.serializer.Serialize(transfer);
            dbreezeTransaction.Insert<byte[], byte[]>(DepositTableName, transfer.DepositTransactionId.ToBytes(), depositBytes);
        }

        /// <inheritdoc />
        public Transfer GetTransfer(uint256 depositId)
        {
            using (Transaction dbreezeTransaction = this.db.GetTransaction())
            {
                Row<byte[], byte[]> row = dbreezeTransaction.Select<byte[], byte[]>(DepositTableName, depositId.ToBytes());

                if (!row.Exists)
                    return null;

                return this.serializer.Deserialize<Transfer>(row.Value);
            }
        }

        /// <inheritdoc />
        public IList<Transfer> GetAllTransfers()
        {
            // TODO: Make scalable.

            using (Transaction dbreezeTransaction = this.db.GetTransaction())
            {
                IEnumerable<Row<byte[], byte[]>> rows = dbreezeTransaction.SelectForward<byte[], byte[]>(DepositTableName);

                return rows.Where(x => x.Key.Length == 32).Select(x => this.serializer.Deserialize<Transfer>(x.Value)).ToList();
            }
        }
    }
}

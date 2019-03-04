using System.Collections.Generic;
using NBitcoin;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.TargetChain;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    public interface ITransferRepository
    {
        /// <summary>
        /// Get the highest block number we know about deposits for.
        /// </summary>
        int GetSyncedBlockNumber();

        /// <summary>
        /// Save deposits to disk.
        /// </summary>
        bool SaveDeposits(IList<MaturedBlockDepositsModel> maturedBlockDeposits);

        /// <summary>
        /// Get the saved deposit for a given transaction id.
        /// </summary>
        Transfer GetTransfer(uint256 depositId);

        /// <summary>
        /// Get all the saved deposits. NOTE: Obviously not scalable.
        /// </summary>
        IList<Transfer> GetAllTransfers();
    }
}

using System.Collections.Generic;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public class TransferManager : ITransferManager
    {
        private readonly ITransferRepository transferRepository;

        public TransferManager(ITransferRepository transferRepository)
        {
            this.transferRepository = transferRepository;
        }

        public void QueryDepositsAndMakeTransfers()
        {
            IList<Transfer> deposits = this.transferRepository.GetAllTransfers();

            // for each deposit, check whether it has been seen in a block or has started being built.
        }
    }
}

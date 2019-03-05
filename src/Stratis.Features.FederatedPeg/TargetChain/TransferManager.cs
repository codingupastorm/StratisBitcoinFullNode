using System.Collections.Generic;
using System.Linq;
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

        // Maybe should happen every 10 seconds?
        public void ActOnTransfers()
        {
            IEnumerable<Transfer> transfersByBlockHeight = this.transferRepository.GetAllTransfers().OrderBy(x=>x.BlockHeight);

            // Get the next one to act on. The lowest height deposit that isn't seen in block.
            Transfer toActOn = transfersByBlockHeight.FirstOrDefault(x => x.Status != TransferStatus.SeenInBlock);

            // We're done if there's no pending deposits.
            if (toActOn == null)
                return;


            if (toActOn.Status == TransferStatus.FullySigned)
            {
                // TODO: Attempt to broadcast if it's not already in our mempool (or an equivalent is), then we can move onto another transfer
            }

            if (toActOn.Status == TransferStatus.Partial)
            {
                // Continue sending it around until it is ready? Or just wait... We can't build others whilst this is in progress though.
            }

            if (toActOn.Status == TransferStatus.NotCreated)
            {
                //  Build the transaction and send it round!
            }

        }

        private void BuildAndSendAroundTransaction()
        {

        }
    }
}

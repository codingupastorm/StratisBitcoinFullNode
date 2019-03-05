using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Wallet;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public class TransferManager : ITransferManager
    {
        private readonly Network network;
        private readonly ITransferRepository transferRepository;
        private readonly IWithdrawalTransactionBuilder withdrawalTransactionBuilder;

        public TransferManager(
            Network network,
            ITransferRepository transferRepository,
            IWithdrawalTransactionBuilder withdrawalTransactionBuilder)
        {
            this.network = network;
            this.transferRepository = transferRepository;
            this.withdrawalTransactionBuilder = withdrawalTransactionBuilder;
        }

        // Maybe should happen every 10 seconds?
        public void ProgressTransfers()
        {
            // TODO: Obviously this isn't scalable. Work something out between caching / querying.

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
                throw new NotImplementedException();
            }

            if (toActOn.Status == TransferStatus.NotCreated)
            {
                this.BuildAndSendAroundTransaction(toActOn);
            }

        }

        private void BuildAndSendAroundTransaction(Transfer transfer)
        {
            Script scriptPubKey = BitcoinAddress.Create(transfer.DepositTargetAddress, this.network).ScriptPubKey;

            var recipient = new Recipient
            {
                Amount = transfer.DepositAmount,
                ScriptPubKey = scriptPubKey
            };

            Transaction transaction = this.withdrawalTransactionBuilder.BuildWithdrawalTransaction(transfer.DepositTransactionId, transfer.DepositTime, recipient);


        }
    }
}

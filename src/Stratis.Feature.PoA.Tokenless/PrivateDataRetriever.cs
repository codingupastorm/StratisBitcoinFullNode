using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless.Payloads;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.Core.Store;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface IPrivateDataRetriever
    {
        /// <summary>
        /// Lets the node know about some private data that has arrived in a block, so it can decide whether to ask around for it.
        /// </summary>
        /// <param name="txHash">The hash of the ReadWriteSet transaction that came in a block.</param>
        Task WaitForPrivateData(uint256 txHash);

        /// <summary>
        /// Retrieves the specified transactions from the transient store and then puts them in the private state db.
        /// </summary>
        void MoveDataFromTransientToPrivateStore(List<uint256> txIds);
    }

    public class PrivateDataRetriever : IPrivateDataRetriever
    {
        private readonly ITransientStore transientStore;
        private readonly IPrivateDataStore privateDataStore;
        private readonly ITokenlessBroadcaster tokenlessBroadcaster;

        /// <summary>
        /// How often to go out and request the private data.
        /// </summary>
        private static readonly TimeSpan TimeBetweenQueries = TimeSpans.FiveSeconds;

        /// <summary>
        /// This will hold up the node for a maximum of 25 seconds (5 x 5).
        /// </summary>
        private const int AmountOfTimesToRetry = 5;

        public PrivateDataRetriever(ITransientStore transientStore,
            IPrivateDataStore privateDataStore,
            ITokenlessBroadcaster tokenlessBroadcaster)
        {
            this.transientStore = transientStore;
            this.privateDataStore = privateDataStore;
            this.tokenlessBroadcaster = tokenlessBroadcaster;
        }

        /// <inheritdoc />
        public async Task WaitForPrivateData(uint256 txHash)
        {
            // TODO: Check if this node is allowed to get the data.

            for(int i=0; i< AmountOfTimesToRetry; i++)
            {
                (TransientStorePrivateData Data, uint BlockHeight) item = this.transientStore.Get(txHash);

                if (item.Data != null)
                {
                    // We have the data inside the transient store - we're good to go!
                    return;
                }

                await this.tokenlessBroadcaster.BroadcastToWholeOrganisationAsync(new RequestPrivateDataPayload(txHash))
                    .ConfigureAwait(false);

                Thread.Sleep(TimeBetweenQueries);
            }

            throw new NotImplementedException(
                "Node never received the private data. Need to handle how to progress in this case.");
        }

        public void MoveDataFromTransientToPrivateStore(List<uint256> txIds)
        {
            foreach (uint256 txId in txIds)
            {
                // Get from transient store
                (TransientStorePrivateData Data, uint BlockHeight) item = this.transientStore.Get(txId);
                ReadWriteSet rws = ReadWriteSet.FromJsonEncodedBytes(item.Data.ToBytes());

                // Move to private data store.
                foreach (WriteItem write in rws.Writes)
                {
                    this.privateDataStore.StoreBytes(write.ContractAddress, write.Key, write.Value);
                }

                // TODO: Remove from transient store.
            }
        }
    }
}

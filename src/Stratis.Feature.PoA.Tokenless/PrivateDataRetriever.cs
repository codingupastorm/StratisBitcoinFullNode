using System;
using NBitcoin;
using Stratis.SmartContracts.Core.Store;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface IPrivateDataRetriever
    {
        void StartRetrievalLoop();

        /// <summary>
        /// Lets the node know about some private data that has arrived in a block, so it can decide whether to ask around for it.
        /// </summary>
        /// <param name="txHash">The hash of the ReadWriteSet transaction that came in a block.</param>
        void RegisterNewPrivateData(uint256 txHash);
    }

    public class PrivateDataRetriever : IPrivateDataRetriever
    {
        private readonly ITransientStore transientStore;

        public PrivateDataRetriever(ITransientStore transientStore)
        {
            this.transientStore = transientStore;
        }

        /// <inheritdoc />
        public void RegisterNewPrivateData(uint256 txHash)
        {
            // TODO: Check if this node is allowed to get the data.

            if (this.transientStore.Get(txHash) != null)
            {
                // We have the data!
                // TODO: In this case, we should put the data into the private data store. Coming in a future PR.
                return;
            }

            // We don't have the data - put it in transient store with block height 0 so we know to retrieve it later on.
            throw new NotImplementedException("For the next PR.");
        }

        public void StartRetrievalLoop()
        {
            throw new NotImplementedException("For the next PR.");
        }
    }
}

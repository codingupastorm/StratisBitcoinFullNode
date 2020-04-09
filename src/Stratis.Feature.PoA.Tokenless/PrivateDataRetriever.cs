using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Utilities;
using Stratis.Feature.PoA.Tokenless.Payloads;
using Stratis.SmartContracts.Core.Store;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface IPrivateDataRetriever
    {
        /// <summary>
        /// Starts a loop that periodically retrieves data.
        /// </summary>
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
        private readonly IMissingPrivateDataStore missingPrivateDataStore;
        private readonly IAsyncProvider asyncProvider;
        private readonly INodeLifetime nodeLifetime;
        private readonly ITokenlessBroadcaster tokenlessBroadcaster;
        private IAsyncLoop asyncLoop;

        /// <summary>
        /// How often to trigger the query to request private data. Increase if there are performance issues.
        /// If this is ridicul
        /// </summary>
        private static readonly TimeSpan TimeBetweenQueries = TimeSpans.TenSeconds;

        public PrivateDataRetriever(ITransientStore transientStore,
            IMissingPrivateDataStore missingPrivateDataStore,
            ITokenlessBroadcaster tokenlessBroadcaster,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime)
        {
            this.transientStore = transientStore;
            this.missingPrivateDataStore = missingPrivateDataStore;
            this.asyncProvider = asyncProvider;
            this.nodeLifetime = nodeLifetime;
            this.tokenlessBroadcaster = tokenlessBroadcaster;
        }

        /// <inheritdoc />
        public void RegisterNewPrivateData(uint256 txHash)
        {
            // TODO: Check if this node is allowed to get the data.

            if (this.transientStore.Get(txHash).Data != null)
            {
                // We have the data!
                // TODO: In this case, we should put the data into the private data store. Coming in a future PR.
                return;
            }

            // We don't have the data - put it in a store so we know to retrieve it later on.
            this.missingPrivateDataStore.Add(txHash);
        }

        public void StartRetrievalLoop()
        {
            this.asyncLoop = this.asyncProvider.CreateAndRunAsyncLoop(nameof(PrivateDataRetriever), async token => {
                    await this.RetrievePrivateData().ConfigureAwait(false);
                },
                this.nodeLifetime.ApplicationStopping,
                TimeBetweenQueries);
        }

        private async Task RetrievePrivateData()
        {
            // Get all of the incomplete private data entries.
            IEnumerable<uint256> entries = this.missingPrivateDataStore.GetMissingEntries();

            foreach (uint256 txId in entries)
            {
                await this.tokenlessBroadcaster.BroadcastToWholeOrganisationAsync(new RequestPrivateDataPayload(txId))
                    .ConfigureAwait(false);
            }
        }
    }
}

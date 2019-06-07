﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.NetworkHelpers;
using Stratis.Features.FederatedPeg.Payloads;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    /// <summary>
    /// Requests partial transactions from the peers and calls <see cref="ICrossChainTransferStore.MergeTransactionSignaturesAsync".
    /// </summary>
    public interface IPartialTransactionRequester {
        /// <summary>
        /// Starts the broadcasting of partial transaction requests.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the broadcasting of partial transaction requests.
        /// </summary>
        void Stop();
    }

    /// <inheritdoc />
    public class PartialTransactionRequester : IPartialTransactionRequester {
        /// <summary>
        /// How many transactions we want to pass around to sign at a time.
        /// </summary>
        private const int NumberToSignAtATime = 3;

        /// <summary>
        /// How often to trigger the query for and broadcasting of partial transactions.
        /// </summary>
        private static readonly TimeSpan TimeBetweenQueries = TimeSpans.TenSeconds;

        private readonly ILogger logger;
        private readonly ICrossChainTransferStore crossChainTransferStore;
        private readonly IAsyncProvider asyncProvider;
        private readonly INodeLifetime nodeLifetime;
        private readonly IFederatedPegBroadcaster federatedPegBroadcaster;

        private readonly IInitialBlockDownloadState ibdState;
        private readonly IFederationWalletManager federationWalletManager;

        private IAsyncLoop asyncLoop;

        public PartialTransactionRequester(
            ILoggerFactory loggerFactory,
            ICrossChainTransferStore crossChainTransferStore,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifetime,
            IFederatedPegBroadcaster federatedPegBroadcaster,
            IInitialBlockDownloadState ibdState,
            IFederationWalletManager federationWalletManager) {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(crossChainTransferStore, nameof(crossChainTransferStore));
            Guard.NotNull(asyncProvider, nameof(asyncProvider));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.crossChainTransferStore = crossChainTransferStore;
            this.asyncProvider = asyncProvider;
            this.nodeLifetime = nodeLifetime;
            this.ibdState = ibdState;
            this.federatedPegBroadcaster = federatedPegBroadcaster;
            this.federationWalletManager = federationWalletManager;
        }

        public async Task BroadcastPartialTransactionsAsync() {
            if (this.ibdState.IsInitialBlockDownload() || !this.federationWalletManager.IsFederationWalletActive()) {
                this.logger.LogTrace("Federation wallet isn't active or in IBD. Not attempting to request transaction signatures.");
                return;
            }

            // Broadcast the partial transaction with the earliest inputs.
            IEnumerable<ICrossChainTransfer> transfers = this.crossChainTransferStore.GetTransfersByStatus(new[] {CrossChainTransferStatus.Partial}, true).Take(NumberToSignAtATime);

            foreach (ICrossChainTransfer transfer in transfers)
            {
                await this.federatedPegBroadcaster.BroadcastAsync(new RequestPartialTransactionPayload(transfer.DepositTransactionId).AddPartial(transfer.PartialTransaction));
                this.logger.LogDebug("Partial template requested for deposit ID {0}", transfer.DepositTransactionId);
            }
        }

        /// <inheritdoc />
        public void Start() {
            this.asyncLoop = this.asyncProvider.CreateAndRunAsyncLoop(nameof(PartialTransactionRequester), async token => {
                await this.BroadcastPartialTransactionsAsync().ConfigureAwait(false);
            },
            this.nodeLifetime.ApplicationStopping,
            TimeBetweenQueries);
        }

        /// <inheritdoc />
        public void Stop() {
            if (this.asyncLoop != null) {
                this.asyncLoop.Dispose();
                this.asyncLoop = null;
            }
        }
    }
}

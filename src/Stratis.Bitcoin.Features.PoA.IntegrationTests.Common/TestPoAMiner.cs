﻿using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Validators;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests.Common
{
    public class TestPoAMiner : PoAMiner
    {
        private readonly EditableTimeProvider timeProvider;

        private readonly CancellationTokenSource cancellation;

        private readonly ISlotsManager slotsManager;

        public TestPoAMiner(
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            Network network,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState ibdState,
            BlockDefinition blockDefinition,
            ISlotsManager slotsManager,
            IConnectionManager connectionManager,
            PoABlockHeaderValidator poaHeaderValidator,
            IFederationManager federationManager,
            IIntegrityValidator integrityValidator,
            IWalletManager walletManager,
            INodeStats nodeStats,
            VotingManager votingManager,
            PoAMinerSettings poAMinerSettings,
            IAsyncProvider asyncProvider) : base(consensusManager, dateTimeProvider, network, nodeLifetime, loggerFactory, ibdState, blockDefinition, slotsManager,
                connectionManager, poaHeaderValidator, federationManager, integrityValidator, walletManager, nodeStats, votingManager, poAMinerSettings, asyncProvider)
        {
            this.cancellation = new CancellationTokenSource();
            this.timeProvider = dateTimeProvider as EditableTimeProvider;
            this.slotsManager = slotsManager;
        }

        public override void InitializeMining()
        {
        }

        public async Task MineBlocksAsync(int count)
        {
            for (int i = 0; i < count; i++)
            {
                this.timeProvider.AdjustedTimeOffset += TimeSpan.FromSeconds(
                    this.slotsManager.GetRoundLengthSeconds(this.federationManager.GetFederationMembers().Count));

                uint timeNow = (uint)this.timeProvider.GetAdjustedTimeAsUnixTimestamp();

                uint myTimestamp = this.slotsManager.GetMiningTimestamp(timeNow);

                this.timeProvider.AdjustedTimeOffset += TimeSpan.FromSeconds(myTimestamp - timeNow);

                ChainedHeader chainedHeader = await this.MineBlockAtTimestampAsync(myTimestamp).ConfigureAwait(false);

                if (chainedHeader == null)
                {
                    i--;
                    this.timeProvider.AdjustedTimeOffset += TimeSpan.FromHours(1);
                    continue;
                }

                var builder = new StringBuilder();
                builder.AppendLine("<<==============================================================>>");
                builder.AppendLine($"Block was mined {chainedHeader}.");
                builder.AppendLine("<<==============================================================>>");
                this.logger.LogInformation(builder.ToString());
            }
        }

        public override void Dispose()
        {
            this.cancellation.Cancel();
            base.Dispose();
        }
    }

    public class TokenlessPoAMiner : PoAMiner
    {
        private readonly EditableTimeProvider dateTimeProvider;

        private readonly ISlotsManager slotsManager;

        public TokenlessPoAMiner(
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            Network network,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState ibdState,
            BlockDefinition blockDefinition,
            ISlotsManager slotsManager,
            IConnectionManager connectionManager,
            PoABlockHeaderValidator poaHeaderValidator,
            IFederationManager federationManager,
            IIntegrityValidator integrityValidator,
            IWalletManager walletManager,
            INodeStats nodeStats,
            PoAMinerSettings poAMinerSettings,
            IAsyncProvider asyncProvider)
            : base(consensusManager, dateTimeProvider, network, nodeLifetime, loggerFactory, ibdState, blockDefinition, slotsManager, connectionManager, poaHeaderValidator, federationManager, integrityValidator, walletManager, nodeStats, null, poAMinerSettings, asyncProvider)
        {
            this.dateTimeProvider = dateTimeProvider as EditableTimeProvider;
            this.slotsManager = slotsManager;
        }

        public override void InitializeMining()
        {
        }

        public async Task MineBlocksAsync(int count)
        {
            for (int i = 0; i < count; i++)
            {
                this.dateTimeProvider.AdjustedTimeOffset += TimeSpan.FromSeconds(
                    this.slotsManager.GetRoundLengthSeconds(this.federationManager.GetFederationMembers().Count));

                uint timeNow = (uint)this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp();

                uint myTimestamp = this.slotsManager.GetMiningTimestamp(timeNow);

                this.dateTimeProvider.AdjustedTimeOffset += TimeSpan.FromSeconds(myTimestamp - timeNow);

                ChainedHeader chainedHeader = await this.MineBlockAtTimestampAsync(myTimestamp).ConfigureAwait(false);

                if (chainedHeader == null)
                {
                    i--;
                    this.dateTimeProvider.AdjustedTimeOffset += TimeSpan.FromHours(1);
                    continue;
                }

                var builder = new StringBuilder();
                builder.AppendLine("<<==============================================================>>");
                builder.AppendLine($"Block was mined {chainedHeader}.");
                builder.AppendLine("<<==============================================================>>");
                this.logger.LogInformation(builder.ToString());
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}

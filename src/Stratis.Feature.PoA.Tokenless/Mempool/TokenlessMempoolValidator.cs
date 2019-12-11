using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Feature.PoA.Tokenless.Mempool
{
    /// <summary>
    /// Validates memory pool transactions.
    /// </summary>
    public class TokenlessMempoolValidator : IMempoolValidator
    {
        private readonly ChainIndexer chainIndexer;
        private readonly ICoinView coinView;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly ILogger logger;
        private readonly ITxMempool mempool;
        private readonly MempoolSchedulerLock mempoolLock;
        private readonly List<IMempoolRule> mempoolRules;
        private readonly MempoolSettings mempoolSettings;
        private readonly Network network;

        /// <summary>Gets a counter for tracking memory pool performance.</summary>
        public MempoolPerformanceCounter PerformanceCounter { get; }

        /// <summary>Gets the consensus options from the <see cref="CoinViewRule"/></summary>
        public ConsensusOptions ConsensusOptions
        {
            get { return this.network.Consensus.Options; }
        }

        public TokenlessMempoolValidator(
            ChainIndexer chainIndexer,
            ICoinView coinView,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ITxMempool memPool,
            MempoolSchedulerLock mempoolLock,
            IEnumerable<IMempoolRule> mempoolRules,
            MempoolSettings mempoolSettings)
        {
            this.chainIndexer = chainIndexer;
            this.coinView = coinView;
            this.dateTimeProvider = dateTimeProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.mempool = memPool;
            this.mempoolLock = mempoolLock;
            this.mempoolRules = mempoolRules.ToList();
            this.mempoolSettings = mempoolSettings;
            this.network = chainIndexer.Network;
            this.PerformanceCounter = new MempoolPerformanceCounter(this.dateTimeProvider);
        }

        /// <inheritdoc />
        public async Task<bool> AcceptToMemoryPoolWithTime(MempoolValidationState mempoolValidationState, Transaction transaction)
        {
            try
            {
                await this.AcceptToMemoryPoolWorkerAsync(mempoolValidationState, transaction);

                if (mempoolValidationState.IsInvalid)
                {
                    this.logger.LogTrace("(-):false");
                    return false;
                }

                this.logger.LogTrace("(-):true");
                return true;
            }
            catch (MempoolErrorException mempoolError)
            {
                this.logger.LogDebug("{0}:'{1}' ErrorCode:'{2}',ErrorMessage:'{3}'", nameof(MempoolErrorException), mempoolError.Message, mempoolError.ValidationState?.Error?.Code, mempoolError.ValidationState?.ErrorMessage);
                this.logger.LogTrace("(-)[MEMPOOL_EXCEPTION]:false");
                return false;
            }
            catch (ConsensusErrorException consensusError)
            {
                this.logger.LogDebug("{0}:'{1}' ErrorCode:'{2}',ErrorMessage:'{3}'", nameof(ConsensusErrorException), consensusError.Message, consensusError.ConsensusError?.Code, consensusError.ConsensusError?.Message);
                mempoolValidationState.Error = new MempoolError(consensusError.ConsensusError);
                this.logger.LogTrace("(-)[CONSENSUS_EXCEPTION]:false");
                return false;
            }
        }

        /// <inheritdoc />
        public Task<bool> AcceptToMemoryPool(MempoolValidationState mempoolValidationState, Transaction transaction)
        {
            mempoolValidationState.AcceptTime = this.dateTimeProvider.GetTime();
            return this.AcceptToMemoryPoolWithTime(mempoolValidationState, transaction);
        }

        /// <inheritdoc />
        public Task SanityCheck()
        {
            return this.mempoolLock.ReadAsync(() => this.mempool.Check(this.coinView));
        }

        /// <summary>
        /// Validates and then adds a transaction to memory pool.
        /// </summary>
        /// <param name="mempoolValidationState">Validation state for creating the validation context.</param>
        /// <param name="transaction">The transaction to validate.</param>
        private async Task AcceptToMemoryPoolWorkerAsync(MempoolValidationState mempoolValidationState, Transaction transaction)
        {
            var context = new TokenlessMempoolValidationContext(transaction, mempoolValidationState);

            this.PreMempoolChecks(context);

            // Create the MemPoolCoinView and load relevant utxoset
            context.View = new MempoolCoinView(this.coinView, this.mempool, this.mempoolLock, this);

            // Adding to the mem pool can only be done sequentially so use the sequential scheduler for that.
            await this.mempoolLock.WriteAsync(() =>
            {
                context.View.LoadViewLocked(context.Transaction);

                // If the transaction already exists in the mempool,
                // we only record the state but do not throw an exception.
                // This is because the caller will check if the state is invalid
                // and if so return false, meaning that the transaction should not be relayed.
                if (this.mempool.Exists(context.TransactionHash))
                {
                    mempoolValidationState.Invalid(MempoolErrors.InPool);
                    this.logger.LogTrace("(-)[INVALID_TX_ALREADY_EXISTS]");
                    return;
                }

                // Execute all the mempool rules.
                foreach (IMempoolRule rule in this.mempoolRules)
                {
                    rule.CheckTransaction(context);
                }

                // Add the validated transaction in to mempool.
                this.mempool.AddUnchecked(context.TransactionHash, context.Entry, context.SetAncestors);

                // Trim mempool and check if transaction was trimmed.
                if (!mempoolValidationState.OverrideMempoolLimit)
                {
                    this.LimitMempoolSize(this.mempoolSettings.MaxMempool * 1000000, this.mempoolSettings.MempoolExpiry * 60 * 60);

                    if (!this.mempool.Exists(context.TransactionHash))
                    {
                        this.logger.LogTrace("(-)[FAIL_MEMPOOL_FULL]");
                        mempoolValidationState.Fail(MempoolErrors.Full).Throw();
                    }
                }

                // Do this here inside the exclusive scheduler for better accuracy and to avoid springing more concurrent tasks later.
                mempoolValidationState.MempoolSize = this.mempool.Size;
                mempoolValidationState.MempoolDynamicSize = this.mempool.DynamicMemoryUsage();

                this.PerformanceCounter.SetMempoolSize(mempoolValidationState.MempoolSize);
                this.PerformanceCounter.SetMempoolDynamicSize(mempoolValidationState.MempoolDynamicSize);
                this.PerformanceCounter.AddHitCount(1);
            });
        }

        /// <summary>
        /// Checks that are done before touching the memory pool.
        /// These checks don't need to run under the memory pool lock.
        /// </summary>
        /// <param name="validationContext">Current validation context.</param>
        private void PreMempoolChecks(TokenlessMempoolValidationContext validationContext)
        {
            // Only accept nLockTime-using transactions that can be mined in the next
            // block; we don't want our mempool filled up with transactions that can't
            // be mined yet.
            if (!MempoolValidator.CheckFinalTransaction(this.chainIndexer, this.dateTimeProvider, validationContext.Transaction, MempoolValidator.StandardLocktimeVerifyFlags))
            {
                this.logger.LogTrace("(-)[FAIL_NONSTANDARD]");
                validationContext.State.Fail(MempoolErrors.NonFinal).Throw();
            }
        }

        /// <summary>
        /// Trims memory pool to a new size.
        /// First expires transactions older than age.
        /// Then trims memory pool to limit if necessary.
        /// </summary>
        /// <param name="limit">New size.</param>
        /// <param name="age">AAge to use for calculating expired transactions.</param>
        private void LimitMempoolSize(long limit, long age)
        {
            int expired = this.mempool.Expire(this.dateTimeProvider.GetTime() - age);
            if (expired != 0)
                this.logger.LogInformation($"Expired {expired} transactions from the memory pool");

            var vNoSpendsRemaining = new List<uint256>();
            this.mempool.TrimToSize(limit, vNoSpendsRemaining);
        }
    }
}

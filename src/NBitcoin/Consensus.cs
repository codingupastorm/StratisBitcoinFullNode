using System;
using System.Collections.Generic;
using NBitcoin.BouncyCastle.Math;

namespace NBitcoin
{
    public class Consensus : IConsensus
    {
        /// <inheritdoc />
        public Money PremineReward { get; }

        /// <inheritdoc />
        public long PremineHeight { get; }

        /// <inheritdoc />
        public Money ProofOfStakeReward { get; }

        /// <inheritdoc />
        public uint MaxReorgLength { get; private set; }

        public ConsensusOptions Options { get; set; }

        public BuriedDeploymentsArray BuriedDeployments { get; }

        public IBIP9DeploymentsArray BIP9Deployments { get; }

        public int MajorityEnforceBlockUpgrade { get; }

        public int MajorityRejectBlockOutdated { get; }

        public int MajorityWindow { get; }

        public uint256 BIP34Hash { get; }

        /// <inheritdoc />
        public bool PosNoRetargeting { get; }

        public uint256 HashGenesisBlock { get; }

        /// <inheritdoc />
        public uint256 MinimumChainWork { get; }

        /// <inheritdoc />
        public int CoinType { get; }

        public BigInteger ProofOfStakeLimit { get; }

        public BigInteger ProofOfStakeLimitV2 { get; }

        /// <inheritdoc />
        public bool IsProofOfStake { get; }

        /// <inheritdoc />
        public uint256 DefaultAssumeValid { get; }

        /// <inheritdoc />
        public ConsensusFactory ConsensusFactory { get; }

        /// <inheritdoc />
        public ConsensusRules ConsensusRules { get; }

        /// <inheritdoc />
        public List<Type> MempoolRules { get; set; }

        public IConsensusProofOfWork ConsensusProofOfWork { get; set; }

        public Consensus(
            ConsensusFactory consensusFactory,
            ConsensusOptions consensusOptions,
            int coinType,
            uint256 hashGenesisBlock,
            int majorityEnforceBlockUpgrade,
            int majorityRejectBlockOutdated,
            int majorityWindow,
            BuriedDeploymentsArray buriedDeployments,
            IBIP9DeploymentsArray bip9Deployments,
            uint256 bip34Hash,
            uint maxReorgLength,
            uint256 defaultAssumeValid,
            long premineHeight,
            Money premineReward,
            bool posNoRetargeting,
            uint256 minimumChainWork,
            bool isProofOfStake,
            BigInteger proofOfStakeLimit,
            BigInteger proofOfStakeLimitV2,
            Money proofOfStakeReward,
            IConsensusProofOfWork consensusProofOfWork = null)
        {
            this.PremineReward = premineReward;
            this.PremineHeight = premineHeight;
            this.ProofOfStakeReward = proofOfStakeReward;
            this.MaxReorgLength = maxReorgLength;
            this.Options = consensusOptions;
            this.BuriedDeployments = buriedDeployments;
            this.BIP9Deployments = bip9Deployments;
            this.MajorityEnforceBlockUpgrade = majorityEnforceBlockUpgrade;
            this.MajorityRejectBlockOutdated = majorityRejectBlockOutdated;
            this.MajorityWindow = majorityWindow;
            this.BIP34Hash = bip34Hash;
            this.PosNoRetargeting = posNoRetargeting;
            this.HashGenesisBlock = hashGenesisBlock;
            this.MinimumChainWork = minimumChainWork;
            this.CoinType = coinType;
            this.ProofOfStakeLimit = proofOfStakeLimit;
            this.ProofOfStakeLimitV2 = proofOfStakeLimitV2;
            this.IsProofOfStake = isProofOfStake;
            this.DefaultAssumeValid = defaultAssumeValid;
            this.ConsensusFactory = consensusFactory;
            this.ConsensusRules = new ConsensusRules();
            this.MempoolRules = new List<Type>();

            this.ConsensusProofOfWork = consensusProofOfWork;
        }
    }

    public class ConsensusProofOfWork : IConsensusProofOfWork
    {
        /// <inheritdoc />
        public long CoinbaseMaturity { get; set; }

        public int LastPOWBlock { get; set; }

        /// <inheritdoc />
        public long MaxMoney { get; set; }

        public int MinerConfirmationWindow { get; set; }

        public bool PowAllowMinDifficultyBlocks { get; set; }

        public Target PowLimit { get; set; }

        public bool PowNoRetargeting { get; set; }

        public TimeSpan PowTargetSpacing { get; set; }

        public TimeSpan PowTargetTimespan { get; set; }

        public Money ProofOfWorkReward { get; set; }

        public int SubsidyHalvingInterval { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Serialization;

namespace NBitcoin
{
    public class Consensus : IConsensus
    {
        /// <inheritdoc />
        [JsonPropertyName("maxreorglength")]
        public uint MaxReorgLength { get; set; }

        [JsonPropertyName("options")]
        [JsonConverter(typeof(JsonConsensusOptionsConverter))]
        public ConsensusOptions Options { get; set; }

        public BuriedDeploymentsArray BuriedDeployments { get; }

        public IBIP9DeploymentsArray BIP9Deployments { get; }

        public uint256 BIP34Hash { get; }

        public uint256 HashGenesisBlock { get; set; }

        /// <inheritdoc />
        public uint256 MinimumChainWork { get; set; }

        /// <inheritdoc />
        [JsonPropertyName("cointype")]
        public int CoinType { get; set; }

        /// <inheritdoc />
        [JsonPropertyName("defaultassumevalid")]
        public uint256 DefaultAssumeValid { get; set; }

        /// <inheritdoc />
        public ConsensusFactory ConsensusFactory { get; set; }

        /// <inheritdoc />
        public ConsensusRules ConsensusRules { get; set; }

        /// <inheritdoc />
        public List<Type> MempoolRules { get; set; }

        public IConsensusMiningReward ConsensusMiningReward { get; set; }

        public Consensus()
        {
            this.BIP9Deployments = new NoBIP9Deployments();
            this.BuriedDeployments = new BuriedDeploymentsArray();
        }

        public Consensus(
            ConsensusFactory consensusFactory,
            ConsensusOptions consensusOptions,
            int coinType,
            uint256 hashGenesisBlock,
            BuriedDeploymentsArray buriedDeployments,
            IBIP9DeploymentsArray bip9Deployments,
            uint256 bip34Hash,
            uint maxReorgLength,
            uint256 defaultAssumeValid,
            uint256 minimumChainWork,
            IConsensusMiningReward consensusProofOfWork = null)
        {
            this.MaxReorgLength = maxReorgLength;
            this.Options = consensusOptions;
            this.BuriedDeployments = buriedDeployments;
            this.BIP9Deployments = bip9Deployments;
            this.BIP34Hash = bip34Hash;
            this.HashGenesisBlock = hashGenesisBlock;
            this.MinimumChainWork = minimumChainWork;
            this.CoinType = coinType;
            this.DefaultAssumeValid = defaultAssumeValid;
            this.ConsensusFactory = consensusFactory;
            this.ConsensusRules = new ConsensusRules();
            this.MempoolRules = new List<Type>();

            this.ConsensusMiningReward = consensusProofOfWork;
        }
    }

    public class ConsensusProofOfWork : IConsensusMiningReward
    {
        /// <inheritdoc />
        public long CoinbaseMaturity { get; set; }

        public bool IsProofOfStake { get; set; }

        public int LastPOWBlock { get; set; }

        /// <inheritdoc />
        public long MaxMoney { get; set; }

        public int MinerConfirmationWindow { get; set; }

        public bool PosNoRetargeting { get; set; }

        public bool PowAllowMinDifficultyBlocks { get; set; }

        public Target PowLimit { get; set; }

        public bool PowNoRetargeting { get; set; }

        public TimeSpan PowTargetSpacing { get; set; }

        public TimeSpan PowTargetTimespan { get; set; }

        public Money PremineReward { get; set; }

        public long PremineHeight { get; set; }

        public BigInteger ProofOfStakeLimit { get; set; }

        public BigInteger ProofOfStakeLimitV2 { get; set; }

        public Money ProofOfStakeReward { get; set; }

        public Money ProofOfWorkReward { get; set; }

        public int SubsidyHalvingInterval { get; set; }
    }
}
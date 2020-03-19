using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using NBitcoin.BouncyCastle.Math;

namespace NBitcoin
{
    public interface IConsensus
    {
        [JsonIgnore]
        IConsensusMiningReward ConsensusMiningReward { get; set; }

        /// <summary>
        /// Maximal length of reorganization that the node is willing to accept, or 0 to disable long reorganization protection.
        /// </summary>
        [JsonPropertyName("maxreorglength")]
        uint MaxReorgLength { get; set; }

        [JsonPropertyName("options")]
        ConsensusOptions Options { get; set; }

        [JsonIgnore]
        BuriedDeploymentsArray BuriedDeployments { get; }

        [JsonIgnore]
        IBIP9DeploymentsArray BIP9Deployments { get; }

        [JsonIgnore]
        uint256 BIP34Hash { get; }

        [JsonIgnore]
        uint256 HashGenesisBlock { get; }

        /// <summary> The minimum amount of work the best chain should have. </summary>
        [JsonIgnore]
        uint256 MinimumChainWork { get; }

        /// <summary>
        /// Specify the BIP44 coin type for this network.
        /// </summary>
        [JsonPropertyName("cointype")]
        int CoinType { get; set; }

        /// <summary>The default hash to use for assuming valid blocks.</summary>
        [JsonPropertyName("defaultassumevalid")]
        uint256 DefaultAssumeValid { get; set; }

        /// <summary>
        /// A factory that enables overloading base types.
        /// </summary>
        [JsonIgnore]
        ConsensusFactory ConsensusFactory { get; }

        /// <summary>Group of rules that define a given network.</summary>
        [JsonIgnore]
        ConsensusRules ConsensusRules { get; }

        /// <summary>Group of mempool validation rules used by the given network.</summary>
        [JsonIgnore]
        List<Type> MempoolRules { get; set; }
    }

    /// <summary>
    /// These consensus properties relates to proof-of-stake and proof-of-work algorithm where mining rewards are expected.
    /// </summary>
    public interface IConsensusMiningReward
    {
        /// <inheritdoc />
        long CoinbaseMaturity { get; set; }

        /// <summary>
        /// An indicator whether this is a Proof Of Stake network.
        /// </summary>
        bool IsProofOfStake { get; }

        /// <inheritdoc />
        long MaxMoney { get; set; }

        /// <summary>PoW blocks are not accepted after block with height <see cref="Consensus.LastPOWBlock"/>.</summary>
        int LastPOWBlock { get; set; }

        int MinerConfirmationWindow { get; set; }

        /// <summary>
        /// If <c>true</c> disables checking the next block's difficulty (work required) target on a Proof-Of-Stake network.
        /// <para>
        /// This can be used in tests to enable fast mining of blocks.
        /// </para>
        /// </summary>
        bool PosNoRetargeting { get; }

        /// <summary>
        /// Amount of coins mined when a new network is bootstrapped.
        /// Set to <see cref="Money.Zero"/> when there is no premine.
        /// </summary>
        Money PremineReward { get; }

        /// <summary>
        /// The height of the block in which the pre-mined coins should be.
        /// Set to 0 when there is no premine.
        /// </summary>
        long PremineHeight { get; }

        BigInteger ProofOfStakeLimit { get; }

        BigInteger ProofOfStakeLimitV2 { get; }

        /// <summary>
        /// The reward that goes to the miner when a block is mined using proof-of-stake.
        /// </summary>
        Money ProofOfStakeReward { get; }

        bool PowAllowMinDifficultyBlocks { get; }

        Target PowLimit { get; set; }

        /// <summary>
        /// If <c>true</c> disables checking the next block's difficulty (work required) target on a Proof-Of-Work network.
        /// <para>
        /// This can be used in tests to enable fast mining of blocks.
        /// </para>
        /// </summary>
        bool PowNoRetargeting { get; set; }

        TimeSpan PowTargetSpacing { get; set; }

        TimeSpan PowTargetTimespan { get; set; }

        /// <summary> The reward that goes to the miner when a block is mined using proof-of-work.</summary>
        Money ProofOfWorkReward { get; set; }

        int SubsidyHalvingInterval { get; set; }
    }
}
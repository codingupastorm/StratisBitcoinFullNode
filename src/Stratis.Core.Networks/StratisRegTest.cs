﻿using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Protocol;
using Stratis.Core.Networks.Deployments;
using Stratis.Core.Networks.Policies;

namespace Stratis.Core.Networks
{
    public class StratisRegTest : StratisMain
    {
        public StratisRegTest()
        {
            var messageStart = new byte[4];
            messageStart[0] = 0xcd;
            messageStart[1] = 0xf2;
            messageStart[2] = 0xc0;
            messageStart[3] = 0xef;
            uint magic = BitConverter.ToUInt32(messageStart, 0); // 0xefc0f2cd

            this.Name = "StratisRegTest";
            this.NetworkType = NetworkType.Regtest;
            this.Magic = magic;
            this.DefaultPort = 18444;
            this.DefaultMaxOutboundConnections = 16;
            this.DefaultMaxInboundConnections = 109;
            this.DefaultAPIPort = 38221;
            this.CoinTicker = "TSTRAT";
            this.DefaultBanTimeSeconds = 16000; // 500 (MaxReorg) * 64 (TargetSpacing) / 2 = 4 hours, 26 minutes and 40 seconds

            var powLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));

            var consensusFactory = new PosConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1470467000;
            this.GenesisNonce = 1831645;
            this.GenesisBits = 0x1e0fffff;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            Block genesisBlock = CreateStratisGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);

            genesisBlock.Header.Time = 1494909211;
            genesisBlock.Header.Nonce = 2433759;
            genesisBlock.Header.Bits = powLimit;

            this.Genesis = genesisBlock;

            // Taken from StratisX.
            var consensusOptions = new PosConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000,
                maxStandardTxSigopsCost: 20_000 / 5
            );

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 0,
                [BuriedDeployments.BIP65] = 0,
                [BuriedDeployments.BIP66] = 0
            };

            var bip9Deployments = new StratisBIP9Deployments()
            {
                // Always active on StratisRegTest.
                [StratisBIP9Deployments.ColdStaking] = new BIP9DeploymentsParameters("ColdStaking", 1, BIP9DeploymentsParameters.AlwaysActive, 999999999, BIP9DeploymentsParameters.AlwaysActive)
            };

            var consensusProofOfWork = new ConsensusProofOfWork()
            {
                CoinbaseMaturity = 10,
                IsProofOfStake = true,
                LastPOWBlock = 12500,
                MaxMoney = long.MaxValue,
                MinerConfirmationWindow = 2016,
                PosNoRetargeting = true,
                PowAllowMinDifficultyBlocks = true,
                PowLimit = powLimit,
                PowNoRetargeting = true,
                PowTargetSpacing = TimeSpan.FromSeconds(10 * 60),
                PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                PremineHeight = 2,
                PremineReward = Money.Coins(98000000),
                ProofOfStakeLimit = new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                ProofOfStakeLimitV2 = new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                ProofOfStakeReward = Money.COIN,
                ProofOfWorkReward = Money.Coins(4),
                SubsidyHalvingInterval = 210000,
            };

            this.Consensus = new NBitcoin.Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 105,
                hashGenesisBlock: genesisBlock.GetHash(),
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"),
                maxReorgLength: 500,
                defaultAssumeValid: null, // turn off assumevalid for regtest.
                minimumChainWork: null,
                consensusProofOfWork: consensusProofOfWork
            );

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (65) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (65 + 128) };

            this.Checkpoints = new Dictionary<int, CheckpointInfo>()
            {
                // Fake checkpoint to prevent PH to be activated.
                // TODO: Once PH is complete, this should be removed
                // { 100_000 , new CheckpointInfo(uint256.Zero, uint256.Zero) }
            };
            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();

            this.StandardScriptsRegistry = new StratisStandardScriptsRegistry();

            // 64 below should be changed to TargetSpacingSeconds when we move that field.
            Assert(this.DefaultBanTimeSeconds <= this.Consensus.MaxReorgLength * 64 / 2);
            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x93925104d664314f581bc7ecb7b4bad07bcfabd1cfce4256dbd2faddcf53bd1f"));

            this.RegisterRules(this.Consensus);
            this.RegisterMempoolRules(this.Consensus);
        }
    }
}
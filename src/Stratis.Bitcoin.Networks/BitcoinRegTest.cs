using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Networks.Deployments;
using Stratis.Bitcoin.Networks.Policies;

namespace Stratis.Bitcoin.Networks
{
    public class BitcoinRegTest : BitcoinMain
    {
        public BitcoinRegTest()
        {
            this.Name = "RegTest";
            this.AdditionalNames = new List<string> { "reg" };
            this.NetworkType = NetworkType.Regtest;
            this.Magic = 0xDAB5BFFA;
            this.DefaultPort = 18444;
            this.DefaultMaxOutboundConnections = 8;
            this.DefaultMaxInboundConnections = 117;
            this.DefaultAPIPort = 38220;
            this.CoinTicker = "TBTC";
            this.DefaultBanTimeSeconds = 60 * 60 * 24; // 24 Hours

            // Create the genesis block.
            this.GenesisTime = 1296688602;
            this.GenesisNonce = 2;
            this.GenesisBits = 0x207fffff;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Coins(50m);

            var consensusFactory = new ConsensusFactory();
            Block genesisBlock = CreateBitcoinGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);

            this.Genesis = genesisBlock;

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 100000000,
                [BuriedDeployments.BIP65] = 100000000,
                [BuriedDeployments.BIP66] = 100000000
            };

            var bip9Deployments = new BitcoinBIP9Deployments
            {
                [BitcoinBIP9Deployments.TestDummy] = new BIP9DeploymentsParameters("TestDummy", 28, 0, 999999999, BIP9DeploymentsParameters.DefaultRegTestThreshold),
                [BitcoinBIP9Deployments.CSV] = new BIP9DeploymentsParameters("CSV", 0, 0, 999999999, BIP9DeploymentsParameters.DefaultRegTestThreshold),
                [BitcoinBIP9Deployments.Segwit] = new BIP9DeploymentsParameters("Segwit", 1, BIP9DeploymentsParameters.AlwaysActive, 999999999, BIP9DeploymentsParameters.AlwaysActive)
            };

            var consensusProofOfWork = new ConsensusProofOfWork()
            {
                CoinbaseMaturity = 100,
                IsProofOfStake = false,
                LastPOWBlock = default,
                MaxMoney = 21000000 * Money.COIN,
                MinerConfirmationWindow = 144,
                PosNoRetargeting = false,
                PowAllowMinDifficultyBlocks = true,
                PowLimit = new Target(new uint256("7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")),
                PowNoRetargeting = true,
                PowTargetSpacing = TimeSpan.FromSeconds(10 * 60),
                PowTargetTimespan = TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks,
                PremineHeight = 0,
                PremineReward = Money.Zero,
                ProofOfStakeLimit = null,
                ProofOfStakeLimitV2 = null,
                ProofOfStakeReward = Money.Zero,
                ProofOfWorkReward = Money.Coins(50),
                SubsidyHalvingInterval = 150,
            };

            this.Consensus = new NBitcoin.Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: new ConsensusOptions(), // Default - set to Bitcoin params.
                coinType: 0,
                hashGenesisBlock: genesisBlock.GetHash(),
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256(),
                maxReorgLength: 0,
                defaultAssumeValid: null, // turn off assumevalid for regtest.
                minimumChainWork: uint256.Zero,
                consensusProofOfWork: consensusProofOfWork
            );

            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (111) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (196) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (239) };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x35), (0x87), (0xCF) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x35), (0x83), (0x94) };
            this.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2b };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 115 };

            var encoder = new Bech32Encoder("tb");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();
            this.DNSSeeds = new List<DNSSeedData>();
            this.SeedNodes = new List<NetworkAddress>();

            this.StandardScriptsRegistry = new BitcoinStandardScriptsRegistry();

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x0f9188f13cb7b2c71f2a335e3a4fc328bf5beb436012afca590b1a11466e2206"));

            this.RegisterRules(this.Consensus);
            this.RegisterMempoolRules(this.Consensus);
        }
    }
}
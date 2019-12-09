
using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules;
using Stratis.Bitcoin.Features.PoA.Policies;
using Stratis.Feature.PoA.Tokenless.Consensus.Rules;
using Stratis.Feature.PoA.Tokenless.Core.Rules;
using Stratis.Feature.PoA.Tokenless.Mempool.Rules;

namespace Stratis.Feature.PoA.Tokenless
{
    public sealed class TokenlessNetwork : Network
    {
        /// <summary> The name of the root folder containing the different PoA blockchains.</summary>
        private const string NetworkRootFolderName = "tokenless";

        /// <summary> The default name used for the Stratis configuration file. </summary>
        private const string NetworkDefaultConfigFilename = "tokenless.conf";

        public TokenlessNetwork()
        {
            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            // TODO-TL: Change/update.
            var messageStart = new byte[4];
            messageStart[0] = 0x76;
            messageStart[1] = 0x36;
            messageStart[2] = 0x23;
            messageStart[3] = 0x06;
            uint magic = BitConverter.ToUInt32(messageStart, 0);

            // TODO-TL: Check All
            this.Name = "TokenlessMain";
            this.NetworkType = NetworkType.Mainnet;
            this.Magic = magic;
            this.DefaultPort = 16438;
            this.DefaultMaxOutboundConnections = 16;
            this.DefaultMaxInboundConnections = 109;
            this.DefaultRPCPort = 16474;
            this.DefaultAPIPort = 37221;
            this.MaxTipAge = 2 * 60 * 60;
            this.MinTxFee = 10000;
            this.FallbackFee = 10000;
            this.MinRelayTxFee = 10000;
            this.RootFolderName = NetworkRootFolderName;
            this.DefaultConfigFilename = NetworkDefaultConfigFilename;
            this.MaxTimeOffsetSeconds = 25 * 60;
            this.CoinTicker = "POATL";

            // TODO-TL: Is the CF any different for tokenless?
            var consensusFactory = new PoAConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1513622125;
            this.GenesisNonce = 1560058197;
            this.GenesisBits = 402691653;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            Block genesisBlock = CreateGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion);

            this.Genesis = genesisBlock;

            // Configure federation.
            // Keep in mind that order in which keys are added to this list is important
            // and should be the same for all nodes operating on this network.
            var genesisFederationMembers = new List<IFederationMember>()
            {
                new FederationMember(new PubKey("03025fcadedd28b12665de0542c8096f4cd5af8e01791a4d057f67e2866ca66ba7")),
                new FederationMember(new PubKey("027724a9ecc54417ff0250c3355d300cee008747b630f43e791cd02c2b35294d2f")),
                new FederationMember(new PubKey("022f8ad1799fd281fc9519814d20a407ed120ba84ec24cca8e869b811e6f6d4590"))
            };

            var consensusOptions = new PoAConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000, // TODO-TL: Check
                maxStandardTxSigopsCost: 20_000 / 5, // TODO-TL: Check
                genesisFederationMembers: genesisFederationMembers,
                targetSpacingSeconds: 16,
                votingEnabled: true,
                autoKickIdleMembers: true,
                enablePermissionedMembership: true
            );

            // TODO-TL: Check
            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 0,
                [BuriedDeployments.BIP65] = 0,
                [BuriedDeployments.BIP66] = 0
            };

            var bip9Deployments = new NoBIP9Deployments();

            this.Consensus = new NBitcoin.Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 105, // TODO-TL: Check
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 210000, // TODO-TL: Check
                majorityEnforceBlockUpgrade: 750, // TODO-TL: Check
                majorityRejectBlockOutdated: 950, // TODO-TL: Check
                majorityWindow: 1000, // TODO-TL: Check
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"),
                minerConfirmationWindow: 2016, // TODO-TL: Check
                maxReorgLength: 500, // TODO-TL: Check
                defaultAssumeValid: null,
                maxMoney: long.MaxValue, // TODO-TL: Check
                coinbaseMaturity: 2, // TODO-TL: Check
                premineHeight: 10, // TODO-TL: Check
                premineReward: Money.Coins(100_000_000), // TODO-TL: Check
                proofOfWorkReward: Money.Coins(0), // TODO-TL: Check
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                powTargetSpacing: TimeSpan.FromSeconds(60),
                powAllowMinDifficultyBlocks: false,
                posNoRetargeting: true,
                powNoRetargeting: true,
                powLimit: null,
                minimumChainWork: null,
                isProofOfStake: false,
                lastPowBlock: 0,
                proofOfStakeLimit: null,
                proofOfStakeLimitV2: null,
                proofOfStakeReward: Money.Zero // TODO-TL: Check
            );

            // https://en.bitcoin.it/wiki/List_of_address_prefixes
            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (55) }; // 'P' prefix
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (117) }; // 'p' prefix
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (63 + 128) };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xAD), (0xE4) };
            this.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            this.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            this.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2a };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            this.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            // TODO-TL: Not needed?
            this.Checkpoints = new Dictionary<int, CheckpointInfo>
            {
                { 0, new CheckpointInfo(new uint256("0x0621b88fb7a99c985d695be42e606cb913259bace2babe92970547fa033e4076")) },
            };

            var encoder = new Bech32Encoder("bc");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            // TODO-TL: Not needed?
            this.DNSSeeds = new List<DNSSeedData> { };

            // TODO-TL: Not needed?
            string[] seedNodes = { };
            this.SeedNodes = this.ConvertToNetworkAddresses(seedNodes, this.DefaultPort).ToList();

            // TODO-TL: Not needed?
            this.StandardScriptsRegistry = new PoAStandardScriptsRegistry();

            // TODO-TL: Generate new Genesis
            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("ad3c9c471280e686cd85c156a829caaacd2936a4649b9ca26fff743b1462c551"));
            //Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x9928b372fd9e4cf62a31638607344c03c48731ba06d24576342db9c8591e1432"));

            // TODO-TL: Add Smart Contract State Root Hash

            if ((this.ConsensusOptions.GenesisFederationMembers == null) || (this.ConsensusOptions.GenesisFederationMembers.Count == 0))
                throw new Exception("No keys for initial federation are configured!");

            this.RegisterRules(this.Consensus);
            this.RegisterMempoolRules(this.Consensus);
        }

        public PoAConsensusOptions ConsensusOptions => this.Consensus.Options as PoAConsensusOptions;

        public void RegisterRules(IConsensus consensus)
        {
            // IHeaderValidationConsensusRules
            consensus.ConsensusRules
                .Register<HeaderTimeChecksPoARule>()
                .Register<PoAHeaderDifficultyRule>()
                .Register<PoAHeaderSignatureRule>();

            // IIntegrityValidationConsensusRules
            consensus.ConsensusRules
                .Register<BlockMerkleRootRule>()
                .Register<PoAIntegritySignatureRule>();

            // IPartialValidationConsensusRules
            consensus.ConsensusRules
                .Register<BlockSizeRule>()
                .Register<IsSmartContractWellFormedPartialValidationRule>()
                .Register<CanSmartContractSenderBeRetrievedPartialValidationRule>();

            // IFullValidationConsensusRule
            consensus.ConsensusRules
                .Register<TokenlessCoinviewRule>();
            // ------------------------------------------------------
        }

        // TODO-TL: What other rules are required?
        public void RegisterMempoolRules(IConsensus consensus)
        {
            consensus.MempoolRules = new List<Type>()
            {
                typeof(CreateTokenlessMempoolEntryRule),
                typeof(IsSmartContractWellFormedMempoolRule),
                typeof(CanSmartContractSenderBeRetrievedMempoolRule)
            };
        }

        // TODO-TL: Check no output?
        private Block CreateGenesisBlock(ConsensusFactory consensusFactory, uint time, uint nonce, uint bits, int versio)
        {
            string data = "GenesisBlockForTheNewTokenlessNetwork";

            Transaction transaction = consensusFactory.CreateTransaction();
            transaction.Version = 1;
            transaction.Time = time;
            transaction.AddInput(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(0), new Op()
                {
                    Code = (OpcodeType)0x1,
                    PushData = new[] { (byte)42 }
                }, Op.GetPushOp(Encoders.ASCII.DecodeData(data)))
            });

            Block genesis = consensusFactory.CreateBlock();
            genesis.Header.BlockTime = Utils.UnixTimeToDateTime(time);
            genesis.Header.Bits = bits;
            genesis.Header.Nonce = nonce;
            genesis.Header.Version = versio;
            genesis.Transactions.Add(transaction);
            genesis.Header.HashPrevBlock = uint256.Zero;
            genesis.UpdateMerkleRoot();
            return genesis;
        }
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules;
using Stratis.Bitcoin.Features.PoA.Policies;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Consensus.Rules;
using Stratis.Feature.PoA.Tokenless.Mempool.Rules;
using Stratis.Feature.PoA.Tokenless.Wallet;

namespace Stratis.Feature.PoA.Tokenless
{
    public sealed class TokenlessNetwork : PoANetwork
    {
        /// <summary> The name of the root folder containing the different PoA blockchains.</summary>
        private const string NetworkRootFolderName = "tokenless";

        /// <summary> The default name used for the Stratis configuration file. </summary>
        private const string NetworkDefaultConfigFilename = "tokenless.conf";

        public static Mnemonic[] Mnemonics = 
        {
            new Mnemonic("lava frown leave wedding virtual ghost sibling able mammal liar wide wisdom"),
            new Mnemonic("idle power swim wash diesel blouse photo among eager reward govern menu"),
            new Mnemonic("high neither night category fly wasp inner kitchen phone current skate hair")
        };


        public Key[] FederationKeys { get; private set; }

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
            this.DefaultAPIPort = 30000;
            this.MaxTipAge = 2 * 60 * 60;
            this.RootFolderName = NetworkRootFolderName;
            this.DefaultConfigFilename = NetworkDefaultConfigFilename;
            this.MaxTimeOffsetSeconds = 25 * 60;
            this.CoinTicker = "POATL";

            var consensusFactory = new TokenlessConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1513622125;
            this.GenesisNonce = 1560058197;
            this.GenesisBits = 402691653;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            // TODO-TL: This creates a block that has a transaction in it. We don't need that, right?
            Block genesisBlock = CreateGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion);

            this.Genesis = genesisBlock;

            this.FederationKeys = Mnemonics.Select(m => TokenlessWallet.GetKey(500, m, TokenlessWalletAccount.BlockSigning, 0)).ToArray();

            var genesisFederationMembers = new List<IFederationMember>
            {
                new FederationMember(this.FederationKeys[0].PubKey),
                new FederationMember(this.FederationKeys[1].PubKey),
                new FederationMember(this.FederationKeys[2].PubKey)
            };

            // TODO-TL: Implement new TokenlessConsensusOptions?
            var consensusOptions = new PoAConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                0,
                0,
                genesisFederationMembers: genesisFederationMembers,
                targetSpacingSeconds: 16,
                votingEnabled: true,
                autoKickIdleMembers: false,
                enablePermissionedMembership: true
            );

            this.Consensus = new NBitcoin.Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 500,
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 0,
                majorityEnforceBlockUpgrade: 0,
                majorityRejectBlockOutdated: 0,
                majorityWindow: 0,
                buriedDeployments: new BuriedDeploymentsArray(),
                bip9Deployments: new NoBIP9Deployments(),
                bip34Hash: null,
                minerConfirmationWindow: 0,
                maxReorgLength: 10, // This is really a RegTest class at the moment.
                defaultAssumeValid: null,
                maxMoney: long.MinValue,
                coinbaseMaturity: 2, // TODO-TL: Check
                premineHeight: 0,
                premineReward: Money.Coins(0),
                proofOfWorkReward: Money.Coins(0),
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60),
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
                proofOfStakeReward: Money.Zero
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

            this.Checkpoints = new Dictionary<int, CheckpointInfo>();

            var encoder = new Bech32Encoder("bc");
            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            this.DNSSeeds = new List<DNSSeedData> { };
            this.SeedNodes = this.ConvertToNetworkAddresses(new string[] { }, this.DefaultPort).ToList();

            // TODO-TL: Not needed?
            this.StandardScriptsRegistry = new PoAStandardScriptsRegistry();

            // TODO-TL: Generate new Genesis
            // TODO-TL: This is different now because the consensus factory is different.
            // Assert(this.Consensus.HashGenesisBlock == uint256.Parse("690a702893d30a75739b52d9e707f05e5c7da38df0500aa791468a5e609244ba"));
            //Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x9928b372fd9e4cf62a31638607344c03c48731ba06d24576342db9c8591e1432"));

            // TODO-TL: Add Smart Contract State Root Hash

            if ((consensusOptions.GenesisFederationMembers == null) || (consensusOptions.GenesisFederationMembers.Count == 0))
                throw new Exception("No keys for initial federation are configured!");

            this.RegisterRules(this.Consensus);
            this.RegisterMempoolRules(this.Consensus);
        }

        protected override void RegisterRules(IConsensus consensus)
        {
            // IHeaderValidationConsensusRules
            consensus.ConsensusRules
                .Register<HeaderTimeChecksPoARule>()
                .Register<PoAHeaderDifficultyRule>()
                .Register<TokenlessHeaderSignatureRule>();

            // IIntegrityValidationConsensusRules
            consensus.ConsensusRules
                .Register<BlockMerkleRootRule>()
                .Register<PoAIntegritySignatureRule>();

            // IPartialValidationConsensusRules
            consensus.ConsensusRules
                .Register<TokenlessBlockSizeRule>()
                .Register<IsSmartContractWellFormedPartialValidationRule>()
                .Register<SenderInputPartialValidationRule>();

            // IFullValidationConsensusRule
            consensus.ConsensusRules
                .Register<NoDuplicateTransactionExistOnChainRule>()
                .Register<TokenlessCoinviewRule>();
            // ------------------------------------------------------
        }

        protected override void RegisterMempoolRules(IConsensus consensus)
        {
            consensus.MempoolRules = new List<Type>()
            {
                typeof(NoDuplicateTransactionExistOnChainMempoolRule),
                typeof(CreateTokenlessMempoolEntryRule),
                typeof(IsSmartContractWellFormedMempoolRule),
                typeof(SenderInputMempoolRule)
            };
        }

        // TODO-TL: Check no output?
        private Block CreateGenesisBlock(SmartContractPoAConsensusFactory consensusFactory, uint time, uint nonce, uint bits, int versio)
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

            ((SmartContractPoABlockHeader)genesis.Header).HashStateRoot = new uint256("21B463E3B52F6201C0AD6C991BE0485B6EF8C092E64583FFA655CC1B171FE856");

            return genesis;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using NBitcoin;
using NBitcoin.DataEncoders;
using NBitcoin.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.Feature.PoA.Tokenless.Mempool;

namespace Stratis.Feature.PoA.Tokenless
{
    public sealed class TokenlessNetwork : Network
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

        [JsonPropertyName("federationkeys")]
        public Key[] FederationKeys { get; set; }

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

            this.Name = "TokenlessMain";
            this.NetworkType = NetworkType.Mainnet;
            this.Magic = BitConverter.ToUInt32(messageStart, 0);
            this.DefaultPort = 16438;
            this.DefaultMaxOutboundConnections = 16;
            this.DefaultMaxInboundConnections = 109;
            this.DefaultEnableIpRangeFiltering = false;
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
            this.Genesis = CreateGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion);

            this.FederationKeys = Mnemonics.Select(m => TokenlessKeyStore.GetKey(500, m, TokenlessKeyStoreAccount.BlockSigning, 0)).ToArray();

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
                hashGenesisBlock: this.Genesis.GetHash(),
                buriedDeployments: new BuriedDeploymentsArray(),
                bip9Deployments: new NoBIP9Deployments(),
                bip34Hash: null,
                maxReorgLength: 10,
                defaultAssumeValid: null,
                minimumChainWork: null
            );

            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (55) }; // 'P' prefix
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (63 + 128) };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };

            // TODO-TL: This is different now because the consensus factory is different.
            // Assert(this.Consensus.HashGenesisBlock == uint256.Parse("690a702893d30a75739b52d9e707f05e5c7da38df0500aa791468a5e609244ba"));
            // Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x9928b372fd9e4cf62a31638607344c03c48731ba06d24576342db9c8591e1432"));

            // TODO-TL: Add Smart Contract State Root Hash

            if ((consensusOptions.GenesisFederationMembers == null) || (consensusOptions.GenesisFederationMembers.Count == 0))
                throw new Exception("No keys for initial federation are configured!");

            TokenlessConsensusRuleSet.Create(this);
            TokenlessMempoolRuleSet.Create(this);
        }

        public static ChannelNetwork CreateChannelNetwork(string name, string rootFolderName, long genesisTime)
        {
            var tokenlessNetwork = new TokenlessNetwork();
            Block genesisBlock = tokenlessNetwork.CreateGenesisBlock((TokenlessConsensusFactory)tokenlessNetwork.Consensus.ConsensusFactory, (uint)genesisTime, 0, 0, 0, name);

            var channelNetwork = new ChannelNetwork()
            {
                Base58Prefixes = tokenlessNetwork.Base58Prefixes,
                DefaultAPIPort = tokenlessNetwork.DefaultAPIPort,
                DefaultBanTimeSeconds = tokenlessNetwork.DefaultBanTimeSeconds,
                DefaultConfigFilename = tokenlessNetwork.DefaultConfigFilename,
                DefaultEnableIpRangeFiltering = tokenlessNetwork.DefaultEnableIpRangeFiltering,
                DefaultMaxInboundConnections = tokenlessNetwork.DefaultMaxInboundConnections,
                DefaultMaxOutboundConnections = tokenlessNetwork.DefaultMaxOutboundConnections,
                DefaultPort = tokenlessNetwork.DefaultPort,
                DefaultSignalRPort = tokenlessNetwork.DefaultSignalRPort,
                Magic = tokenlessNetwork.Magic,
                MaxTimeOffsetSeconds = tokenlessNetwork.MaxTimeOffsetSeconds,
                MaxTipAge = tokenlessNetwork.MaxTipAge,
                Name = name,
                NetworkType = NetworkType.Mainnet,
                RootFolderName = rootFolderName
            };

            channelNetwork.Consensus = tokenlessNetwork.Consensus;

            // TODO:TL Override the consensus options for now so that don't
            // start any of the CA functionality on the system channel (for now).
            channelNetwork.Consensus.Options = new PoAConsensusOptions
                (
                channelNetwork.Consensus.Options.MaxBlockBaseSize,
                channelNetwork.Consensus.Options.MaxStandardVersion,
                channelNetwork.Consensus.Options.MaxStandardTxWeight,
                0,
                0,
                ((PoAConsensusOptions)channelNetwork.Consensus.Options).GenesisFederationMembers,
                ((PoAConsensusOptions)channelNetwork.Consensus.Options).TargetSpacingSeconds,
                true,
                false,
                false
                );

            channelNetwork.Genesis = genesisBlock;

            return channelNetwork;
        }

        private Block CreateGenesisBlock(TokenlessConsensusFactory consensusFactory, uint time, uint nonce, uint bits, int version, string data = "")
        {
            if (string.IsNullOrEmpty(data))
                data = "GenesisBlockForTheNewTokenlessNetwork";

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
            genesis.Header.Version = version;
            genesis.Transactions.Add(transaction);
            genesis.Header.HashPrevBlock = uint256.Zero;
            genesis.UpdateMerkleRoot();

            ((SmartContractPoABlockHeader)genesis.Header).HashStateRoot = new uint256("21B463E3B52F6201C0AD6C991BE0485B6EF8C092E64583FFA655CC1B171FE856");

            return genesis;
        }
    }
}

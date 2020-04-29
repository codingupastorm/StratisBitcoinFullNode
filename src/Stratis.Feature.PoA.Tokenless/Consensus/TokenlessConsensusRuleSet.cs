using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus.Rules;
using Stratis.Features.Consensus.Rules.CommonRules;
using Stratis.Features.PoA.BasePoAFeatureConsensusRules;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    public static class TokenlessConsensusRuleSet
    {
        public static void Create(Network network)
        {
            network.Consensus.ConsensusRules = new ConsensusRules();

            // IHeaderValidationConsensusRules
            network.Consensus.ConsensusRules
                .Register<HeaderTimeChecksPoARule>()
                .Register<PoAHeaderDifficultyRule>()
                .Register<TokenlessHeaderSignatureRule>();

            // IIntegrityValidationConsensusRules
            network.Consensus.ConsensusRules
                .Register<BlockMerkleRootRule>()
                .Register<PoAIntegritySignatureRule>();

            // IPartialValidationConsensusRules
            network.Consensus.ConsensusRules
                .Register<TokenlessBlockSizeRule>()
                .Register<IsSmartContractWellFormedPartialValidationRule>()
                .Register<SenderInputPartialValidationRule>();

            // IFullValidationConsensusRule
            network.Consensus.ConsensusRules
                .Register<NoDuplicateTransactionExistOnChainRule>()
                .Register<TokenlessCoinviewRule>();
        }

        public static void CreateForSystemChannel(ChannelNetwork channelNetwork)
        {
            channelNetwork.Consensus.ConsensusRules = new ConsensusRules();
            channelNetwork.Consensus.ConsensusRules.Register<ExecuteChannelCreationRequest>();
            channelNetwork.Consensus.ConsensusRules.Register<ExecuteChannelAddMemberRequest>();
        }
    }
}

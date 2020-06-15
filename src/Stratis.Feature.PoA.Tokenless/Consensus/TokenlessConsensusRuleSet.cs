﻿using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Consensus.Rules;
using Stratis.Feature.PoA.Tokenless.Networks;
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
                .Register<CheckChannelUpdateRequestSenderHasPermission>()
                .Register<TokenlessBlockSizeRule>()
                .Register<IsSmartContractWellFormedPartialValidationRule>()
                .Register<SenderInputPartialValidationRule>()
                .Register<EndorsedContractTransactionConsensusRule>();

            // IFullValidationConsensusRule
            network.Consensus.ConsensusRules
                .Register<NoDuplicateTransactionExistOnChainRule>()
                .Register<TokenlessCoinviewRule>();
        }

        public static void CreateForSystemChannel(SystemChannelNetwork network)
        {
            network.Consensus.ConsensusRules = new ConsensusRules();
            network.Consensus.ConsensusRules.Register<CheckChannelCreationRequestSenderHasPermission>();
            network.Consensus.ConsensusRules.Register<CheckChannelUpdateRequestSenderHasPermission>();
            network.Consensus.ConsensusRules.Register<ExecuteChannelCreationRequest>();
        }
    }
}

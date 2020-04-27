using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Mempool.Rules;

namespace Stratis.Feature.PoA.Tokenless.Mempool
{
    public static class TokenlessMempoolRuleSet
    {
        public static void Create(Network network)
        {
            network.Consensus.MempoolRules = new List<Type>()
            {
                typeof(CheckSenderCertificateIsNotRevoked),
                typeof(NoDuplicateTransactionExistOnChainMempoolRule),
                typeof(CreateTokenlessMempoolEntryRule),
                typeof(IsSmartContractWellFormedMempoolRule),
                typeof(SenderInputMempoolRule)
            };
        }

        public static void Create(ChannelNetwork channelNetwork, bool isSystemChannelNode)
        {
            Create(channelNetwork);

            if (isSystemChannelNode)
            {
                channelNetwork.Consensus.MempoolRules.Add(typeof(IsChannelCreationRequestWellFormed));
                channelNetwork.Consensus.MempoolRules.Add(typeof(IsChannelAddMemberRequestWellFormed));
            }
        }
    }
}

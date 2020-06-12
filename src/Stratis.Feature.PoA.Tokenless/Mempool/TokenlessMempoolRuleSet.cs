﻿using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Feature.PoA.Tokenless.Mempool.Rules;
using Stratis.Feature.PoA.Tokenless.Networks;

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
                typeof(SenderInputMempoolRule),
                typeof(ValidateEndorsementsMempoolRule),
                typeof(IsChannelUpdateRequestWellFormed)
            };
        }

        public static void CreateForSystemChannel(SystemChannelNetwork network)
        {
            network.Consensus.MempoolRules = new List<Type>
            {
                typeof(CreateTokenlessMempoolEntryRule),
                typeof(IsChannelCreationRequestWellFormed),
                typeof(IsChannelUpdateRequestWellFormed)
            };
        }
    }
}

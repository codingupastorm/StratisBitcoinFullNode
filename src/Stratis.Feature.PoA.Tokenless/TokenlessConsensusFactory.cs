﻿using NBitcoin;
using Stratis.Bitcoin.Features.SmartContracts.PoA;

namespace Stratis.Feature.PoA.Tokenless
{
    public class TokenlessConsensusFactory : SmartContractPoAConsensusFactory
    {
        /// <inheritdoc />
        public override Transaction CreateTransaction()
        {
            return new TokenlessTransaction();
        }

        /// <inheritdoc />
        public override Transaction CreateTransaction(string hex)
        {
            return new TokenlessTransaction(hex);
        }

        /// <inheritdoc />
        public override Transaction CreateTransaction(byte[] bytes)
        {
            return new TokenlessTransaction(bytes);
        }
    }
}

using Stratis.Bitcoin.Consensus;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    public static class TokenlessPoAConsensusErrors
    {
        public static ConsensusError DuplicateTransaction => new ConsensusError("duplicate-transaction", "transaction has already been seen in a previous block.");

    }
}

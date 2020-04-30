using Stratis.Bitcoin.Consensus;

namespace Stratis.Feature.PoA.Tokenless.Consensus
{
    public static class TokenlessPoAConsensusErrors
    {
        public static ConsensusError DuplicateTransaction => new ConsensusError("duplicate-transaction", "transaction has already been seen in a previous block.");

        public static ConsensusError InvalidChannelCreationRequestEndorsement => new ConsensusError("invalid-channel-creation-request-endorsements", "The provided endorsement signatures do not match the channel creation request data or endorsement policy.");
    }
}

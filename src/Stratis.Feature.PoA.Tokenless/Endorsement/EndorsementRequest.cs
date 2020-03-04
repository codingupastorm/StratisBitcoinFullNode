using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public class EndorsementRequest
    {
        public Transaction ContractTransaction { get; set; }

        // TODO: Put transient data in here.
        public byte[] TransientData { get; set; }
    }
}

using NBitcoin;
using Stratis.Features.PoA;

namespace Stratis.Features.SmartContracts.PoA
{
    /// <summary>
    /// Overrides block header creation for smart contracts in a PoA network.
    /// </summary>
    public class SmartContractPoAConsensusFactory : PoAConsensusFactory
    {
        /// <inheritdoc />
        public override BlockHeader CreateBlockHeader()
        {
            return new SmartContractPoABlockHeader();
        }
    }
}

using NBitcoin;
using Stratis.Bitcoin.Features.PoA;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    public class SmartContractPoAConsensusFactory : PoAConsensusFactory
    {
        /// <inheritdoc />
        public override BlockHeader CreateBlockHeader()
        {
            return new SmartContractPoABlockHeader();
        }
    }

    public class SmartContractCollateralPoAConsensusFactory : CollateralPoAConsensusFactory
    {
        /// <inheritdoc />
        public override BlockHeader CreateBlockHeader()
        {
            return new SmartContractPoABlockHeader();
        }
    }

    public class TokenlessContractConsensusFactory : SmartContractPoAConsensusFactory
    {
        // I don't like this, but it's the fastest way to include timestamp in a transaction on a network.
        // The reality is that serialization should be the responsibility of separate classes. Not hardcoded onto Transaction.
        // And thus different serialization methods would be injectable.

        /// <inheritdoc />
        public override Transaction CreateTransaction()
        {
            return new PosTransaction();
        }

        /// <inheritdoc />
        public override Transaction CreateTransaction(string hex)
        {
            return new PosTransaction(hex);
        }

        /// <inheritdoc />
        public override Transaction CreateTransaction(byte[] bytes)
        {
            return new PosTransaction(bytes);
        }
    }
}

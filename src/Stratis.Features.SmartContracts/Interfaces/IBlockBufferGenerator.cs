using Stratis.Bitcoin.Mining;

namespace Stratis.Features.SmartContracts.Interfaces
{
    /// <summary>
    /// Works out how much room needs to be left at the start of the block as a buffer for protocol transactions.
    /// </summary>
    public interface IBlockBufferGenerator
    {
        BlockDefinitionOptions GetOptionsWithBuffer(BlockDefinitionOptions options);
    }
}

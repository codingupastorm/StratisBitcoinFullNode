using Stratis.Bitcoin.Interfaces;

namespace Stratis.Features.SmartContracts
{
    public sealed class SmartContractVersionProvider : IVersionProvider
    {
        /// <inheritdoc />
        public string GetVersion()
        {
            return "1.0.2.0";
        }
    }
}

using System;
using Stratis.SmartContracts.Tokenless;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Used to inject the contract type for a particular network.
    /// </summary>
    public sealed class ContractBaseTypeHolder
    {
        public Type ContractBaseType { get; }

        public ContractBaseTypeHolder(Type type)
        {
            if (type != typeof(SmartContract) && type != typeof(TokenlessSmartContract))
                throw new ArgumentException("Contract type is not known. Developers may remove this check when using a custom class, but be aware that this is not explicitly supported.");

            this.ContractBaseType = type;
        }
    }
}

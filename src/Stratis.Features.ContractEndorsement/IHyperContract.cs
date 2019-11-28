using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.HyperContracts;
using Stratis.SmartContracts.CLR;

namespace Stratis.Features.ContractEndorsement
{
    public interface IHyperContract
    {
        /// <summary>
        /// The address of the contract.
        /// </summary>
        uint160 Address { get; }

        /// <summary>
        /// The <see cref="Type"/> of the contract instance.
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// The state representing the execution context for the contract.
        /// </summary>
        IHyperContractState State { get; }

        /// <summary>
        /// Invokes the contract's constructor with the types matching the ordered types of the given parameters.
        /// </summary>
        IContractInvocationResult InvokeConstructor(IReadOnlyList<object> parameters);

        /// <summary>
        /// Invokes a method on the contract with the types matching the ordered types of the given parameters.
        /// </summary>
        IContractInvocationResult Invoke(MethodCall call);
    }
}

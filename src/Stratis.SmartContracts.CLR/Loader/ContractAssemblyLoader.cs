using System;
using System.Reflection;
using CSharpFunctionalExtensions;

namespace Stratis.SmartContracts.CLR.Loader
{
    /// <summary>
    /// Loads assemblies from bytecode.
    /// </summary>
    public class ContractAssemblyLoader : ILoader
    {
        private readonly Type contractBaseType;

        public ContractAssemblyLoader(ContractBaseTypeHolder contractBaseTypeHolder)
        {
            this.contractBaseType = contractBaseTypeHolder?.ContractBaseType;
        }

        /// <summary>
        /// Loads a contract from a raw byte array into an anonymous <see cref="AssemblyLoadContext"/>.
        /// </summary>
        public Result<IContractAssembly> Load(ContractByteCode bytes)
        {
            try
            {
                Assembly assembly = Assembly.Load(bytes.Value);

                return Result.Ok<IContractAssembly>(new ContractAssembly(assembly, this.contractBaseType));
            }
            catch (BadImageFormatException e)
            {
                return Result.Fail<IContractAssembly>(e.Message);
            }
        }
    }
}

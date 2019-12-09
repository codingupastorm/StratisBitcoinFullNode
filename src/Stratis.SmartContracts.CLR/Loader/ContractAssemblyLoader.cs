using System;
using System.Reflection;
using CSharpFunctionalExtensions;

namespace Stratis.SmartContracts.CLR.Loader
{
    /// <summary>
    /// Loads assemblies from bytecode.
    /// </summary>
    public class ContractAssemblyLoader<T> : ILoader
    {
        /// <summary>
        /// Loads a contract from a raw byte array into an anonymous <see cref="AssemblyLoadContext"/>.
        /// </summary>
        public Result<IContractAssembly> Load(ContractByteCode bytes)
        {
            try
            {
                Assembly assembly = Assembly.Load(bytes.Value);

                return Result.Ok<IContractAssembly>(new ContractAssembly(assembly, typeof(T)));
            }
            catch (BadImageFormatException e)
            {
                return Result.Fail<IContractAssembly>(e.Message);
            }
        }
    }
}

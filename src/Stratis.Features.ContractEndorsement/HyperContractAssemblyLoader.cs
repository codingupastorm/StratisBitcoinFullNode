using System;
using System.Reflection;
using System.Runtime.Loader;
using CSharpFunctionalExtensions;
using Stratis.SmartContracts.CLR.Loader;

namespace Stratis.Features.ContractEndorsement
{
    public class HyperContractAssemblyLoader : ILoader
    {
        /// <summary>
        /// Loads a contract from a raw byte array into an anonymous <see cref="AssemblyLoadContext"/>.
        /// </summary>
        public Result<IContractAssembly> Load(ContractByteCode bytes)
        {
            try
            {
                Assembly assembly = Assembly.Load(bytes.Value);

                return Result.Ok<IContractAssembly>(new HyperContractAssembly(assembly));
            }
            catch (BadImageFormatException e)
            {
                return Result.Fail<IContractAssembly>(e.Message);
            }
        }
    }
}

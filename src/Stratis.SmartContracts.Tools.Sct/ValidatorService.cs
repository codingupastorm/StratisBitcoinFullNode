﻿using System.Linq;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.CLR.Validation;
using Stratis.SmartContracts.Tools.Sct.Validation;

namespace Stratis.SmartContracts.Tools.Sct
{
    /// <summary>
    /// Provides a quick validation that the contract is being built and invoked correctly.
    /// Separate from <see cref="Validator"/> because Validator will give detailed
    /// information about why a contract is invalid.
    /// </summary>
    public static class ValidatorService
    {
        public static ValidationServiceResult Validate(string fileName, ContractCompilationResult compilationResult, IConsole console, string[] parameters)
        {
            var validationServiceResult = new ValidationServiceResult();

            byte[] compilation = compilationResult.Compilation;
            IContractModuleDefinition moduleDefinition = BuildModuleDefinition(console, compilation);

            console.WriteLine($"Validating file {fileName}...");

            Assembly smartContract = Assembly.Load(compilation);

            // Network does not matter here as we are only checking the deserialized Types of the params.
            var serializer = new MethodParameterStringSerializer(new TokenlessNetwork());
            object[] methodParameters = null;
            if (parameters.Length != 0)
            {
                methodParameters = serializer.Deserialize(parameters);
            }

            // The following generic parameter isn't important
            validationServiceResult.ConstructorExists = Contract<SmartContract>.ConstructorExists(smartContract.ExportedTypes.FirstOrDefault(), methodParameters);

            if (!validationServiceResult.ConstructorExists)
            {
                console.WriteLine("Smart contract construction failed.");
                console.WriteLine("No constructor exists with the provided parameters.");
            }

            validationServiceResult.DeterminismValidationResult = new SctDeterminismValidator().Validate(moduleDefinition);
            validationServiceResult.FormatValidationResult = new SmartContractFormatValidator().Validate(moduleDefinition.ModuleDefinition);
            if (!validationServiceResult.DeterminismValidationResult.IsValid || !validationServiceResult.FormatValidationResult.IsValid)
                console.WriteLine("Smart Contract failed validation. Run validate [FILE] for more info.");

            console.WriteLine();

            return validationServiceResult;
        }

        private static IContractModuleDefinition BuildModuleDefinition(IConsole console, byte[] compilation)
        {
            console.WriteLine("Building ModuleDefinition...");
            IContractModuleDefinition moduleDefinition = ContractDecompiler.GetModuleDefinition(compilation, new DotNetCoreAssemblyResolver()).Value;
            console.WriteLine("ModuleDefinition built successfully.");
            console.WriteLine();
            return moduleDefinition;
        }
    }

    public sealed class ValidationServiceResult
    {
        public ContractCompilationResult CompilationResult { get; set; }
        public SmartContractValidationResult DeterminismValidationResult { get; set; }
        public SmartContractValidationResult FormatValidationResult { get; set; }
        public bool ConstructorExists { get; set; }

        public bool Success
        {
            get
            {
                return
                     this.CompilationResult.Success &&
                     this.FormatValidationResult.IsValid &&
                     this.DeterminismValidationResult.IsValid &&
                     this.ConstructorExists;
            }
        }
    }
}
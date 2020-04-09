using System;
using System.IO;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using NBitcoin;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.Core.State;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    /// <summary>
    /// Controller for dynamically generating swagger documents for smart contract assemblies.
    /// </summary>
    [Route("swagger/contracts")]
    public class ContractSwaggerController : Controller
    {
        private readonly ILoader loader;
        private readonly IStateRepositoryRoot stateRepository;
        private readonly Network network;
        private readonly SwaggerGeneratorOptions options;
        // private readonly JsonSerializer swaggerSerializer;

        public ContractSwaggerController(
            ILoader loader,
            IOptions<SwaggerGeneratorOptions> options,
            IStateRepositoryRoot stateRepository,
            Network network)
        {
            this.loader = loader;
            this.stateRepository = stateRepository;
            this.network = network;
            this.options = options.Value;
        }

        /// <summary>
        /// Dynamically generates a swagger document for the contract at the given address.
        /// </summary>
        /// <param name="address">The contract's address.</param>
        /// <returns>A <see cref="SwaggerDocument"/> model.</returns>
        /// <exception cref="Exception"></exception>
        [Route("{address}")]
        [HttpGet]
        public async Task<IActionResult> ContractSwaggerDoc(string address)
        {
            var code = this.stateRepository.GetCode(address.ToUint160(this.network));

            if (code == null)
                throw new Exception("Contract does not exist");

            Result<IContractAssembly> assemblyLoadResult = this.loader.Load((ContractByteCode) code);

            if (assemblyLoadResult.IsFailure)
                throw new Exception("Error loading assembly");

            IContractAssembly assembly = assemblyLoadResult.Value;

            var swaggerGen = new ContractSwaggerDocGenerator(this.options, address, assembly);

            // Things to do:
            // Get correct wallet parameters
            // Get the parameters in the Generator loading
            // Forward calls from Dynamic...Controller to TokenlessController
            // Get LocalExecutor to work inside TokenlessController

            using (var stringWriter = new StringWriter())
            {
                var jsonWriter = new OpenApiJsonWriter(stringWriter);
                OpenApiDocument doc = swaggerGen.GetSwagger("contracts");
                doc.SerializeAsV3(jsonWriter);

                return Ok(stringWriter.ToString());
            }
        }
    }
}

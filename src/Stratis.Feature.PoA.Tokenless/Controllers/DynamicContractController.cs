using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Feature.PoA.Tokenless.Controllers.Models;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.State;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Stratis.Feature.PoA.Tokenless.Controllers
{
    /// <summary>
    /// Controller for receiving dynamically generated contract calls.
    /// Maps calls from a json object to a request model and proxies this to the correct controller method.
    /// </summary>
    [ApiVersion("1")]
    [Route("api/contract")]
    public class DynamicContractController : Controller
    {
        private readonly TokenlessController tokenlessController;
        private readonly IStateRepositoryRoot stateRoot;
        private readonly ILoader loader;
        private readonly Network network;
        private SwaggerUIOptions config;

        /// <summary>
        /// Creates a new DynamicContractController instance.
        /// </summary>
        /// <param name="localCallController"></param>
        /// <param name="stateRoot"></param>
        /// <param name="loader"></param>
        /// <param name="network"></param>
        public DynamicContractController(
            TokenlessController tokenlessController,
            IStateRepositoryRoot stateRoot,
            ILoader loader,
            SwaggerUIOptions options,
            Network network)
        {
            this.tokenlessController = tokenlessController;
            this.stateRoot = stateRoot;
            this.loader = loader;
            this.network = network;
            this.config = options;
        }

        /// <summary>
        /// Redirects a request to the swagger page for that contract.
        /// </summary>
        /// <param name="address">The contract address</param>
        /// <returns></returns>
        [Route("{address}/swagger")]
        [HttpGet]
        public IActionResult ContractSwaggerUI([FromRoute] string address)
        {
            var builder = new UriBuilder
            {
                Scheme = this.Request.Scheme,
                Host = this.Request.Host.Host
            };

            if (this.Request.Host.Port.HasValue)
                builder.Port = this.Request.Host.Port.Value;

            builder.Path = "/swagger/index.html";

            // This is empirically derived. Seems to be what Swagger UI uses to render the URL where the name matches primaryName.
            builder.Query = $"urls.primaryName={address}";

            var redirectUrl = builder.Uri.ToString();

            // Add the URL to the swagger UI config
            this.config.ConfigObject.Urls = new List<UrlDescriptor>(this.config.ConfigObject.Urls)
            {
                new UrlDescriptor
                {
                    Name = $"{address}", 
                    Url = $"/swagger/contracts/{address}"
                }
            };

            return RedirectPermanent(redirectUrl);
        }

        /// <summary>
        /// Call a method on the contract by broadcasting a call transaction to the network.
        /// </summary>
        /// <param name="address">The address of the contract to call.</param>
        /// <param name="method">The name of the method on the contract being called.</param>
        /// <returns>A model of the transaction data, if created and broadcast successfully.</returns>
        /// <exception cref="Exception"></exception>
        [Route("{address}/method/{method}")]
        [HttpPost]
        public async Task<IActionResult> CallMethod([FromRoute] string address, [FromRoute] string method, [FromBody] JObject requestData)
        {
            var contractCode = this.stateRoot.GetCode(address.ToUint160(this.network));

            if(contractCode == null)
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, "Contract does not exist", $"No contract code found at address {address}");
            
            Result<IContractAssembly> loadResult = this.loader.Load((ContractByteCode) contractCode);

            IContractAssembly assembly = loadResult.Value;

            Type type = assembly.DeployedType;

            MethodInfo methodInfo = type.GetMethod(method);

            if (methodInfo == null)
                throw new Exception("Method does not exist on contract.");

            ParameterInfo[] parameters = methodInfo.GetParameters();

            if (!this.ValidateParams(requestData, parameters))
                throw new Exception("Parameters don't match method signature.");

            // Map the JObject to the parameter + types expected by the call.
            string[] methodParams = parameters.Map(requestData);

            BuildCallContractTransactionModel request = this.MapCallRequest(address, method, methodParams);

            // Build the transaction
            try
            {
                BuildCallContractTransactionResponse transaction = this.tokenlessController.BuildCallContractTransactionCore(request);

                // Proxy to the actual SC controller for broadcasting
                return await this.tokenlessController.SendTransactionAsync(new SendTransactionModel { TransactionHex = transaction.Hex });
            }
            catch (Exception ex)
            {
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, ex.Message, ex.ToString());
            }
        }

        /// <summary>
        /// Query the value of a property on the contract using a local call.
        /// </summary>
        /// <param name="address">The address of the contract to query.</param>
        /// <param name="property">The name of the property to query.</param>
        /// <returns>A model of the query result.</returns>
        [Route("{address}/property/{property}")]
        [HttpGet]
        public IActionResult LocalCallProperty([FromRoute] string address, [FromRoute] string property)
        {
            TokenlessLocalCallModel request = this.MapLocalCallRequest(address, property);

            // Proxy to the tokenless controller
            return this.tokenlessController.LocalCallSmartContractTransaction(request);
        }

        private bool ValidateParams(JObject requestData, ParameterInfo[] parameters)
        {
            foreach (ParameterInfo param in parameters)
            {
                if (requestData[param.Name] == null)
                    return false;
            }

            return true;
        }

        private BuildCallContractTransactionModel MapCallRequest(string address, string method, string[] parameters)
        {
            var call = new BuildCallContractTransactionModel
            {
                Address = address,
                MethodName = method,
                Parameters = parameters
            };

            return call;
        }

        private TokenlessLocalCallModel MapLocalCallRequest(string address, string property)
        {
            return new TokenlessLocalCallModel
            {
                Address = address,
                MethodName = property
            };
        }
    }
}
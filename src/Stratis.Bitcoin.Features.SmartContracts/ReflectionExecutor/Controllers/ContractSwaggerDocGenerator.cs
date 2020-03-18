using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.OpenApi.Models;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.SmartContracts.CLR.Loader;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    /// <summary>
    /// Creates swagger documents for a contract assembly.
    /// Maps the methods of a contract and its parameters to a call endpoint.
    /// Maps the properties of a contract to an local call endpoint.
    /// </summary>
    public class ContractSwaggerDocGenerator : ISwaggerProvider
    {
        private readonly string address;
        private readonly IContractAssembly assembly;
        private readonly string defaultWalletName;
        private readonly string defaultSenderAddress;
        private readonly SwaggerGeneratorOptions options;

        public ContractSwaggerDocGenerator(SwaggerGeneratorOptions options, string address, IContractAssembly assembly, string defaultWalletName = "", string defaultSenderAddress = "")
        {
            this.address = address;
            this.assembly = assembly;
            this.defaultWalletName = defaultWalletName;
            this.defaultSenderAddress = defaultSenderAddress;
            this.options = options;
        }

        private IDictionary<string, OpenApiSchema> CreateDefinitions()
        {
            // Creates schema for each of the methods in the contract.
            var schemaFactory = new ContractSchemaFactory();

            return schemaFactory.Map(this.assembly);
        }

        private IDictionary<string, OpenApiPathItem> CreatePathItems(IDictionary<string, OpenApiSchema> schema)
        {
            // Creates path items for each of the methods & properties in the contract + their schema.O

            IEnumerable<MethodInfo> methods = this.assembly.GetPublicMethods();

            var methodPaths = methods
                .ToDictionary(k => $"/api/contract/{this.address}/method/{k.Name}", v => this.CreatePathItem(v, schema));

            IEnumerable<PropertyInfo> properties = this.assembly.GetPublicGetterProperties();

            var propertyPaths = properties
                .ToDictionary(k => $"/api/contract/{this.address}/property/{k.Name}", v => this.CreatePathItem(v));
            
            foreach (KeyValuePair<string, OpenApiPathItem> item in propertyPaths)
            {
                methodPaths[item.Key] = item.Value;
            }

            return methodPaths;
        }

        private OpenApiPathItem CreatePathItem(PropertyInfo propertyInfo)
        {
            var pathItem = new OpenApiPathItem();

            var operation = new OpenApiOperation();

            operation.Tags = new List<OpenApiTag>
            {
                new OpenApiTag
                {
                    Name = propertyInfo.Name
                }
            };

            operation.Tags = new[] { propertyInfo.Name };
            operation.OperationId = propertyInfo.Name;
            operation.Consumes = new[] { "application/json", "text/json", "application/*+json" };
            operation.Parameters = this.GetLocalCallMetadataHeaderParams();

            operation.Responses = new Dictionary<string, OpenApiResponse>
            {
                {"200", new OpenApiResponse {Description = "Success"}}
            };

            pathItem.Get = operation;

            return pathItem;
        }

        private OpenApiPathItem CreatePathItem(MethodInfo methodInfo, IDictionary<string, OpenApiSchema> schema)
        {
            var pathItem = new OpenApiPathItem();

            var operation = new Operation();

            operation.Tags = new[] { methodInfo.Name };
            operation.OperationId = methodInfo.Name;
            operation.Consumes = new[] { "application/json", "text/json", "application/*+json" };


            Openapib
            var bodyParam = new BodyParameter
            {
                Name = methodInfo.Name,
                In = "body",
                Required = true,
                Schema = schema[methodInfo.Name]
            };

            var parameters = new List<IParameter>
            {
                bodyParam
            };

            // Get the extra metadata fields required for a contract transaction and add this as header data.
            // We use headers few reasons:
            // - Compatibility with Swagger, which doesn't support setting multiple body objects.
            // - Preventing collisions with contract method parameter names if we were add them to method invocation body object.
            // - Still somewhat REST-ful vs. adding query params.
            // We add header params after adding the body param so they appear in the correct order.
            parameters.AddRange(this.GetCallMetadataHeaderParams());

            operation.Parameters = parameters;

            operation.Responses = new Dictionary<string, OpenApiResponse>
            {
                {"200", new Response {Description = "Success"}}
            };

            pathItem.Post = operation;

            return pathItem;
        }

        private List<OpenApiParameter> GetLocalCallMetadataHeaderParams()
        {
            return new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "GasPrice",
                    In = "header",
                    Required = true,
                    Content = new Dictionary<string, OpenApiMediaType>
                    {
                        { "number", new OpenApiMediaType(). }
                    },
                    
                    Type = "number",
                    Format = "int64",
                    Minimum = SmartContractMempoolValidator.MinGasPrice,
                    Maximum = SmartContractFormatLogic.GasPriceMaximum,
                    Default = SmartContractMempoolValidator.MinGasPrice
                },
                new NonBodyParameter
                {
                    Name = "GasLimit",
                    In = "header",
                    Required = true,
                    Type = "number",
                    Format = "int64",
                    Minimum = SmartContractFormatLogic.GasLimitCallMinimum,
                    Maximum = SmartContractFormatLogic.GasLimitMaximum,
                    Default = SmartContractFormatLogic.GasLimitMaximum
                },
                new NonBodyParameter
                {
                    Name = "Amount",
                    In = "header",
                    Required = true,
                    Type = "string",
                    Default = "0"
                },
                new NonBodyParameter
                {
                    Name = "Sender",
                    In = "header",
                    Required = false,
                    Type = "string",
                    Default = this.defaultSenderAddress
                }
            };
        }

        private List<IParameter> GetCallMetadataHeaderParams()
        {
            return new List<IParameter>
            {
                new NonBodyParameter
                {
                    Name = "GasPrice",
                    In = "header",
                    Required = true,
                    Type = "number",
                    Format = "int64",
                    Minimum = SmartContractMempoolValidator.MinGasPrice,
                    Maximum = SmartContractFormatLogic.GasPriceMaximum,
                    Default = SmartContractMempoolValidator.MinGasPrice
                },
                new NonBodyParameter
                {
                    Name = "GasLimit",
                    In = "header",
                    Required = true,
                    Type = "number",
                    Format = "int64",
                    Minimum = SmartContractFormatLogic.GasLimitCallMinimum,
                    Maximum = SmartContractFormatLogic.GasLimitMaximum,
                    Default = SmartContractFormatLogic.GasLimitMaximum
                },
                new NonBodyParameter
                {
                    Name = "Amount",
                    In = "header",
                    Required = true,
                    Type = "string",
                    Default = "0"
                },
                new NonBodyParameter
                {
                    Name = "FeeAmount",
                    In = "header",
                    Required = true,
                    Type = "string",
                    Default = "0.01"
                },
                new NonBodyParameter
                {
                    Name = "WalletName",
                    In = "header",
                    Required = true,
                    Type = "string",
                    Default = this.defaultWalletName
                },
                new NonBodyParameter
                {
                    Name = "WalletPassword",
                    In = "header",
                    Required = true,
                    Type = "string"
                },
                new NonBodyParameter
                {
                    Name = "Sender",
                    In = "header",
                    Required = true,
                    Type = "string",
                    Default = this.defaultSenderAddress
                }
            };
        }

        public OpenApiDocument GetSwagger(string documentName, string host = null, string basePath = null)
        {
            if (!this.options.SwaggerDocs.TryGetValue(documentName, out OpenApiInfo info))
                throw new UnknownSwaggerDocument(documentName, null); // TODO: what goes in this last param?

            IDictionary<string, OpenApiSchema> definitions = this.CreateDefinitions();

            info.Title = $"{this.assembly.DeployedType.Name} Contract API";
            info.Description = $"{this.address}";

            new OpenApiMediaType().

            var swaggerDoc = new OpenApiDocument
            {
                Info = info,
                
                Host = host,
                BasePath = basePath,
                Schemes = schemes,
                Paths = this.CreatePathItems(definitions),
                Definitions = definitions,
                SecurityDefinitions = this.options.SecurityDefinitions.Any() ? this.options.SecurityDefinitions : null,
                Security = this.options.SecurityRequirements.Any() ? this.options.SecurityRequirements : null
            };

            return swaggerDoc;
        }
    }
}
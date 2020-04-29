using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.OpenApi.Models;
using Stratis.SmartContracts.CLR.Loader;
using Stratis.SmartContracts.Tokenless;
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
        private readonly SwaggerGeneratorOptions options;

        public ContractSwaggerDocGenerator(SwaggerGeneratorOptions options, string address, IContractAssembly assembly)
        {
            this.address = address;
            this.assembly = assembly;
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

            IEnumerable<MethodInfo> pureMethods = methods.Where(x => x.CustomAttributes.Any(y => y.AttributeType.Name == typeof(PureAttribute).Name));

            IEnumerable<MethodInfo> normalMethods = methods.Where(x => x.CustomAttributes.All(y => y.AttributeType.Name != typeof(PureAttribute).Name));

            var methodPaths = normalMethods.ToDictionary(k => $"/api/contract/{this.address}/method/{k.Name}", v => this.CreatePathItem(v, schema));

            var pureMethodPaths = pureMethods.ToDictionary(k => $"/api/contract/{this.address}/local-method/{k.Name}", v => this.CreatePathItem(v, schema));

            IEnumerable<PropertyInfo> properties = this.assembly.GetPublicGetterProperties();

            var propertyPaths = properties.ToDictionary(k => $"/api/contract/{this.address}/property/{k.Name}", v => this.CreatePathItem(v));

            // Add them all to one dictionary.

            foreach (KeyValuePair<string, OpenApiPathItem> item in pureMethodPaths)
            {
                methodPaths[item.Key] = item.Value;
            }

            foreach (KeyValuePair<string, OpenApiPathItem> item in propertyPaths)
            {
                methodPaths[item.Key] = item.Value;
            }

            return methodPaths;
        }

        private OpenApiPathItem CreatePathItem(PropertyInfo propertyInfo)
        {
            var operation = new OpenApiOperation
            {
                Tags = new List<OpenApiTag> {new OpenApiTag {Name = propertyInfo.Name}},
                OperationId = propertyInfo.Name,
                Parameters = this.GetLocalCallMetadataHeaderParams(),
                Responses = new OpenApiResponses { { "200", new OpenApiResponse { Description = "Success" } } }
            };

            var pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<OperationType, OpenApiOperation> {{OperationType.Get, operation}}
            };

            return pathItem;
        }

        private OpenApiPathItem CreatePathItem(MethodInfo methodInfo, IDictionary<string, OpenApiSchema> schema)
        {
            var operation = new OpenApiOperation
            {
                Tags = new List<OpenApiTag> { new OpenApiTag { Name = methodInfo.Name } },
                OperationId = methodInfo.Name,
                Parameters = this.GetCallMetadataHeaderParams(),
                Responses = new OpenApiResponses { { "200", new OpenApiResponse { Description = "Success" } } }
            };

            operation.RequestBody = new OpenApiRequestBody
            {
                Description = $"{methodInfo.Name}",
                Required = true,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    { "application/json", new OpenApiMediaType
                        {
                            Schema = schema[methodInfo.Name]
                        }
                    }
                },
            };

            var pathItem = new OpenApiPathItem
            {
                Operations = new Dictionary<OperationType, OpenApiOperation> { { OperationType.Post, operation } }
            };

            return pathItem;
        }

        private List<OpenApiParameter> GetLocalCallMetadataHeaderParams()
        {
            return new List<OpenApiParameter>
            {
            };
        }

        private List<OpenApiParameter> GetCallMetadataHeaderParams()
        {
            return new List<OpenApiParameter>
            {
            };
        }

        public OpenApiDocument GetSwagger(string documentName, string host = null, string basePath = null)
        {
            if (!this.options.SwaggerDocs.TryGetValue(documentName, out OpenApiInfo info))
                throw new UnknownSwaggerDocument(documentName, null); // TODO: what goes in this last param?

            IDictionary<string, OpenApiSchema> definitions = this.CreateDefinitions();

            info.Title = $"{this.assembly.DeployedType.Name} Contract API";
            info.Description = $"{this.address}";

            IDictionary<string, OpenApiPathItem> paths = this.CreatePathItems(definitions);

            OpenApiPaths pathsObject = new OpenApiPaths();

            foreach (KeyValuePair<string, OpenApiPathItem> path in paths)
            {
                pathsObject.Add(path.Key, path.Value);
            }

            var swaggerDoc = new OpenApiDocument
            {
                Info = info,
                Components = new OpenApiComponents
                {
                    Schemas = definitions
                },
                Paths = pathsObject
            };

            return swaggerDoc;
        }
    }
}
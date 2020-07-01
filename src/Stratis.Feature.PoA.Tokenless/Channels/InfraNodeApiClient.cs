using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Stratis.Core.Controllers;

namespace Stratis.Features.Tokenless.Channels
{
    public sealed class InfraNodeApiClient : RestApiClientBase
    {
        public InfraNodeApiClient(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, Uri uri, string controllerName)
            : base(loggerFactory, httpClientFactory, uri, controllerName)
        {
        }
    }
}

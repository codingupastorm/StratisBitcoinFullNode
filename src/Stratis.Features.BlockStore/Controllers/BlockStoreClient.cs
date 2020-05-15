using System.Net.Http;
using Microsoft.Extensions.Logging;
using Stratis.Core.Controllers;

namespace Stratis.Features.BlockStore.Controllers
{
    /// <inheritdoc cref="IBlockStoreClient"/>
    public class BlockStoreClient : RestApiClientBase
    {
        /// <summary>
        /// Currently the <paramref name="url"/> is required as it needs to be configurable for testing.
        /// <para>
        /// In a production/live scenario the sidechain and mainnet federation nodes should run on the same machine.
        /// </para>
        /// </summary>
        public BlockStoreClient(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, string url, int port)
            : base(loggerFactory, httpClientFactory, port, "BlockStore", url)
        {
        }
    }
}
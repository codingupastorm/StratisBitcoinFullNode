using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.Core;
using Stratis.Features.MemoryPool.Broadcasting;

namespace Stratis.Feature.PoA.Tokenless.Controllers
{
    [ApiVersion("1")]
    [ApiController]
    [Route("api/[controller]")]
    public sealed class ChannelsController : Controller
    {
        private readonly ICoreComponent coreComponent;
        private readonly IBroadcasterManager broadcasterManager;
        private readonly ILogger logger;

        public ChannelsController(
            ICoreComponent coreComponent,
            IBroadcasterManager broadcasterManager)
        {
            this.broadcasterManager = broadcasterManager;
            this.coreComponent = coreComponent;
            this.logger = coreComponent.LoggerFactory.CreateLogger(this.GetType());
        }

        [Route("create")]
        [HttpPost]
        public async Task<IActionResult> CreateChannelAsync([FromBody] ChannelCreationRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            try
            {
                var serializer = new ChannelRequestSerializer();
                byte[] serialized = serializer.Serialize(request);
                Transaction transaction = this.coreComponent.Network.CreateTransaction();
                transaction.Outputs.Add(new TxOut(Money.Zero, new Script(serialized)));

                await this.broadcasterManager.BroadcastTransactionAsync(transaction);

                return Ok();
            }
            catch (Exception ex)
            {
                this.logger.LogError("Exception occurred: {0}", ex.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, ex.Message, ex.ToString());
            }
        }
    }
}

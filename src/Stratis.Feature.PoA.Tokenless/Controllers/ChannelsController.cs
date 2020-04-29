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
        private readonly IChannelRepository channelRepository;
        private readonly ICoreComponent coreComponent;
        private readonly IBroadcasterManager broadcasterManager;
        private readonly ILogger logger;

        public ChannelsController(
            IBroadcasterManager broadcasterManager,
            IChannelRepository channelRepository,
            ICoreComponent coreComponent
            )
        {
            this.broadcasterManager = broadcasterManager;
            this.channelRepository = channelRepository;
            this.coreComponent = coreComponent;
            this.logger = coreComponent.LoggerFactory.CreateLogger(this.GetType());
        }

        [Route("create")]
        [HttpPost]
        public async Task<IActionResult> CreateChannelAsync([FromBody] ChannelCreationRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            this.logger.LogInformation($"Request to create channel '{request.Name}' for organisation '{request.Organisation}' received.");

            try
            {
                var serializer = new ChannelRequestSerializer();
                byte[] serialized = serializer.Serialize(request);
                Transaction transaction = this.coreComponent.Network.CreateTransaction();
                transaction.Outputs.Add(new TxOut(Money.Zero, new Script(serialized)));

                this.logger.LogInformation($"Create channel request transaction created.");
                await this.broadcasterManager.BroadcastTransactionAsync(transaction);
                this.logger.LogInformation($"Create channel request transaction broadcasted.");

                return Ok();
            }
            catch (Exception ex)
            {
                this.logger.LogError("Exception occurred: {0}", ex.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, ex.Message, ex.ToString());
            }
        }

        [Route("networkjson")]
        [HttpGet]
        public IActionResult GetNetworkJsonAsync([FromQuery(Name = "cn")] string channelName)
        {
            try
            {
                this.logger.LogInformation($"Request received to retrieve network json for channel '{channelName}'.");
                ChannelDefinition channelDefinition = this.channelRepository.GetChannelDefinition(channelName);
                if (channelDefinition == null)
                {
                    this.logger.LogInformation($"Channel '{channelName}' not found.");
                    return NotFound();
                }

                return Ok(channelDefinition.NetworkJson);
            }
            catch (Exception ex)
            {
                this.logger.LogError("Exception occurred: {0}", ex.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, ex.Message, ex.ToString());
            }
        }
    }
}

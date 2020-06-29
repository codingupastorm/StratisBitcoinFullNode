using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using CertificateAuthority;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Utilities;
using Stratis.Core.Utilities.JsonErrors;
using Stratis.Core.Utilities.ModelStateErrors;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.Consensus;
using Stratis.Feature.PoA.Tokenless.Core;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.Feature.PoA.Tokenless.Models;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.MemoryPool.Broadcasting;

namespace Stratis.Feature.PoA.Tokenless.Controllers
{
    [ApiVersion("1")]
    [ApiController]
    [Route("api/[controller]")]
    public sealed class ChannelsController : Controller
    {
        private readonly IBroadcasterManager broadcasterManager;
        private readonly ICertificatePermissionsChecker certificatePermissionsChecker;
        private readonly IChannelRepository channelRepository;
        private readonly IChannelService channelService;
        private readonly ChannelSettings channelSettings;
        private readonly ICoreComponent coreComponent;
        private readonly ITokenlessKeyStoreManager tokenlessKeyStoreManager;
        private readonly ITokenlessSigner tokenlessSigner;
        private readonly ILogger logger;

        public ChannelsController(
            IBroadcasterManager broadcasterManager,
            ICertificatePermissionsChecker certificatePermissionsChecker,
            IChannelRepository channelRepository,
            IChannelService channelService,
            ChannelSettings channelSettings,
            ICoreComponent coreComponent,
            ITokenlessKeyStoreManager tokenlessKeyStoreManager,
            ITokenlessSigner tokenlessSigner)
        {
            this.broadcasterManager = broadcasterManager;
            this.certificatePermissionsChecker = certificatePermissionsChecker;
            this.channelRepository = channelRepository;
            this.channelService = channelService;
            this.channelSettings = channelSettings;
            this.coreComponent = coreComponent;
            this.tokenlessKeyStoreManager = tokenlessKeyStoreManager;
            this.tokenlessSigner = tokenlessSigner;
            this.logger = coreComponent.LoggerFactory.CreateLogger(this.GetType());
        }

        [Route("create")]
        [HttpPost]
        public async Task<IActionResult> CreateChannelAsync([FromBody] ChannelCreationRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            this.logger.LogInformation($"Request to create channel '{request.Name}' received.");

            if (!this.certificatePermissionsChecker.CheckOwnCertificatePermission(CaCertificatesManager.ChannelCreatePermissionOid))
            {
                this.logger.LogInformation("This peer does not have the permission to create a new channel.");
                return Unauthorized("This peer does not have the permission to create a new channel.");
            }

            try
            {
                await CreateChannelTransactionAsync(request);
                return Ok();
            }
            catch (Exception ex)
            {
                this.logger.LogError("Exception occurred: {0}", ex.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, ex.Message, ex.ToString());
            }
        }

        [Route("update")]
        [HttpPost]
        public async Task<IActionResult> UpdateChannelAsync([FromBody] ChannelUpdateRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            this.logger.LogInformation($"Request to update channel '{request.Name}' received.");

            if (!this.certificatePermissionsChecker.CheckOwnCertificatePermission(CaCertificatesManager.ChannelCreatePermissionOid))
            {
                var message = $"This peer does not have the permission to update channel '{request.Name}'.";
                this.logger.LogInformation(message);
                return Unauthorized(message);
            }

            try
            {
                await CreateChannelTransactionAsync(request);
                return Ok();
            }
            catch (Exception ex)
            {
                this.logger.LogError("Exception occurred: {0}", ex.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, ex.Message, ex.ToString());
            }
        }

        private async Task CreateChannelTransactionAsync<T>(T request)
        {
            var serializer = new ChannelRequestSerializer();
            byte[] serialized = serializer.Serialize(request);
            Transaction transaction = this.coreComponent.Network.CreateTransaction();
            transaction.Outputs.Add(new TxOut(Money.Zero, new Script(serialized)));

            Key key = this.tokenlessKeyStoreManager.LoadTransactionSigningKey();

            this.tokenlessSigner.InsertSignedTxIn(transaction, key.GetBitcoinSecret(this.coreComponent.Network));

            this.logger.LogInformation($"Transaction containing a '{typeof(T).Name}' created.");
            await this.broadcasterManager.BroadcastTransactionAsync(transaction);
            this.logger.LogInformation($"Transaction containing a '{typeof(T).Name}' broadcasted.");
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

        [Route("join")]
        [HttpPost]
        public async Task<IActionResult> JoinChannelAsync([FromBody] ChannelJoinRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            try
            {
                // Record channel membership (in normal node repo) and start up channel node.
                ChannelNetwork network = JsonSerializer.Deserialize<ChannelNetwork>(request.NetworkJson);

                if (request.Port.HasValue)
                {
                    network.DefaultPort = request.Port.Value;
                }

                if (request.ApiPort.HasValue)
                {
                    network.DefaultAPIPort = request.ApiPort.Value;
                }

                if (request.SignalRPort.HasValue)
                {
                    network.DefaultSignalRPort = request.SignalRPort.Value;
                }

                // Note that we don't check if we are allowed to join the network.
                // The network's AccessControlList may have changed from what it was in the initial json, to allow us to join.
                this.logger.LogInformation($"Request to join channel '{network.Name}' received.");

                await this.channelService.JoinChannelAsync(network);

                return Ok();
            }
            catch (Exception ex)
            {
                this.logger.LogError("Exception occurred: {0}", ex.ToString());
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, ex.Message, ex.ToString());
            }
        }

        [Route("systemchanneladdresses")]
        [HttpGet]
        public IActionResult RetrieveSystemChannelAddresses()
        {
            var model = new SystemChannelAddressesModel();

            if (this.channelSettings.IsSystemChannelNode)
            {
                model.Addresses.AddRange(this.channelSettings.SystemChannelNodeAddresses.Select(s => s.ToString()));
                this.logger.LogDebug($"'{model.Addresses.Count}' system channel addresses retrieved.");
            }

            return this.Json(model);
        }
    }
}
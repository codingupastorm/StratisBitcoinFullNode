using System;
using System.Net;
using System.Threading.Tasks;
using MembershipServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Channels.Requests;
using Stratis.Feature.PoA.Tokenless.Core;
using Stratis.Feature.PoA.Tokenless.KeyStore;
using Stratis.Features.MemoryPool.Broadcasting;
using Stratis.Features.PoA.ProtocolEncryption;

namespace Stratis.Feature.PoA.Tokenless.Controllers
{
    [ApiVersion("1")]
    [ApiController]
    [Route("api/[controller]")]
    public sealed class ChannelsController : Controller
    {
        private readonly ICoreComponent coreComponent;
        private readonly IBroadcasterManager broadcasterManager;
        private readonly ChannelSettings channelSettings;
        private readonly IChannelService channelService;
        private readonly ITokenlessKeyStoreManager tokenlessKeyStoreManager;
        private readonly IMembershipServicesDirectory membershipServicesDirectory;
        private readonly IRevocationChecker revocationChecker;
        private readonly ILogger logger;

        public ChannelsController(
            ICoreComponent coreComponent,
            IBroadcasterManager broadcasterManager,
            ChannelSettings channelSettings,
            IChannelService channelService,
            ITokenlessKeyStoreManager tokenlessKeyStoreManager,
            IMembershipServicesDirectory membershipServicesDirectory,
            IRevocationChecker revocationChecker)
        {
            this.broadcasterManager = broadcasterManager;
            this.coreComponent = coreComponent;
            this.channelSettings = channelSettings;
            this.channelService = channelService;
            this.tokenlessKeyStoreManager = tokenlessKeyStoreManager;
            this.membershipServicesDirectory = membershipServicesDirectory;
            this.revocationChecker = revocationChecker;
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

        [Route("join")]
        [HttpPost]
        public async Task<IActionResult> JoinChannelAsync([FromBody] ChannelJoinRequest request)
        {
            if (!this.ModelState.IsValid)
                return ModelStateErrors.BuildErrorResponse(this.ModelState);

            this.logger.LogInformation($"Request to join channel '{request.Name}' received.");

            try
            {
                // Must be a "normal" node.
                if (this.channelSettings.IsChannelNode || this.channelSettings.IsInfraNode ||
                    this.channelSettings.IsSystemChannelNode)
                {
                    throw new InvalidOperationException("Only normal nodes can receive channel join requests.");
                }

                // The channel id is required.
                if (request.Id == 0)
                {
                    throw new InvalidOperationException("The channel id can't be 0.");
                }

                // Get transaction signing pubkey for this node.
                PubKey member = this.tokenlessKeyStoreManager.GetPubKey(TokenlessKeyStoreAccount.TransactionSigning);

                byte[] pubKeyHash = member.Hash.ToBytes();

                X509Certificate x509Certificate = null;

                if (!this.revocationChecker.IsCertificateRevokedByTransactionSigningKeyHash(pubKeyHash))
                    x509Certificate = this.membershipServicesDirectory.GetCertificateForTransactionSigningPubKeyHash(pubKeyHash);

                if (x509Certificate == null)
                {
                    throw new InvalidOperationException("This node's certificate has been revoked.");
                }

                // TODO: Record channel membership (in normal node repo) and start up channel node.
                this.channelService.CreateAndStartChannelNodeAsync(new ChannelCreationRequest()
                {
                    Name = request.Name,
                    Id = request.Id,
                    Organisation = x509Certificate.
                });

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

using System;
using CertificateAuthority;
using MembershipServices;
using NBitcoin;
using Org.BouncyCastle.X509;
using Stratis.Feature.PoA.Tokenless.AccessControl;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Networks;
using Stratis.Features.PoA;

namespace Stratis.Feature.PoA.Tokenless
{
    public class ClientCertificateValidator : IClientCertificateValidator
    {
        private readonly ChannelSettings channelSettings;
        private readonly Network network;
        private readonly IChannelAccessValidator channelAccessValidator;

        public ClientCertificateValidator(ChannelSettings channelSettings, Network network, IChannelAccessValidator channelAccessValidator)
        {
            this.channelSettings = channelSettings;
            this.network = network;
            this.channelAccessValidator = channelAccessValidator;
        }

        public void ConfirmCertificatePermittedOnChannel(X509Certificate certificate)
        {
            // If this is the system channel, check for the system channel permission
            if (this.channelSettings?.IsSystemChannelNode ?? false)
            {
                // If this is the system channel node then the client must have permission to connect.
                byte[] systemChannelPermission = MembershipServicesDirectory.ExtractCertificateExtension(certificate, CaCertificatesManager.SystemChannelPermissionOid);
                if (systemChannelPermission == null || systemChannelPermission.Length != 1 || systemChannelPermission[0] != 1)
                    throw new OperationCanceledException($"The client does not have '{CaCertificatesManager.SystemChannelPermission}' permission.");
            }

            // If this is a created channel, check that the client is allowed to be on there.
            if (this.network is ChannelNetwork channelNetwork)
            {
                if (!this.channelAccessValidator.ValidateCertificateIsPermittedOnChannel(certificate, channelNetwork))
                    throw new OperationCanceledException($"The client is not permitted on this channel.");
            }

        }
    }
}

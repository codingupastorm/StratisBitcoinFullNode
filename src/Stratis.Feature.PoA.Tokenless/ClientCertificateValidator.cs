using System;
using CertificateAuthority;
using Org.BouncyCastle.X509;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.ProtocolEncryption;
using Stratis.Features.PoA;

namespace Stratis.Feature.PoA.Tokenless
{
    public class ClientCertificateValidator : IClientCertificateValidator
    {
        private readonly ChannelSettings channelSettings;

        public ClientCertificateValidator(ChannelSettings channelSettings)
        {
            this.channelSettings = channelSettings;
        }

        public void ConfirmValid(X509Certificate certificate)
        {
            if (this.channelSettings?.IsSystemChannelNode ?? false)
            {
                // If this is the system channel node then the client must have permission to connect.
                byte[] systemChannelPermission = CertificatesManager.ExtractCertificateExtension(certificate, CaCertificatesManager.SystemChannelPermissionOid);
                if (systemChannelPermission == null || systemChannelPermission.Length != 1 || systemChannelPermission[0] != 1)
                    throw new OperationCanceledException($"The client does not have '{CaCertificatesManager.SystemChannelPermission}' permission.");
            }
        }
    }
}

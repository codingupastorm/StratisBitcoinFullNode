using System.Collections.Generic;
using System.Threading.Channels;
using CertificateAuthority;
using Org.BouncyCastle.X509;
using Stratis.Feature.PoA.Tokenless.Channels;
using Stratis.Feature.PoA.Tokenless.Networks;

namespace Stratis.Feature.PoA.Tokenless.AccessControl
{
    public interface IChannelAccessValidator
    {
        bool ValidateCertificateIsPermittedOnChannel(X509Certificate certificate, ChannelNetwork network);
    }

    public class ChannelAccessValidator : IChannelAccessValidator
    {
        private readonly IChannelRepository channelRepository;

        public ChannelAccessValidator(IChannelRepository channelRepository)
        {
            this.channelRepository = channelRepository;
        }

        public bool ValidateCertificateIsPermittedOnChannel(X509Certificate certificate, ChannelNetwork network)
        {
            ChannelDefinition channelDefinition = this.channelRepository.GetChannelDefinition(network.Name);

            // Default to network initial access lists if no access lists are defined, designated by a null value.
            // Important that we do not override these values if the access lists are only empty, as this may be a desired state.
            List<string> organisationAccessList = channelDefinition?.AccessList?.Organisations ?? network.InitialAccessList.Organisations;
            List<string> thumbPrintAccessList = channelDefinition?.AccessList?.Thumbprints ?? network.InitialAccessList.Thumbprints;

            string organisation = certificate.GetOrganisation();

            // Check organisations and thumbprints independently.
            if (organisationAccessList.Contains(organisation))
                return true;

            string thumbprint = CaCertificatesManager.GetThumbprint(certificate);

            return thumbPrintAccessList.Contains(thumbprint);
        }
    }
}

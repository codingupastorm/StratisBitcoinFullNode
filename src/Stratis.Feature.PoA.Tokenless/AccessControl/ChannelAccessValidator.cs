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

            if (channelDefinition?.AccessList?.Organisations == null || channelDefinition.AccessList?.Thumbprints == null)
            {
                return false;
            }

            string organisation = certificate.GetOrganisation();

            if (channelDefinition.AccessList.Organisations.Contains(organisation))
                return true;

            string thumbprint = CaCertificatesManager.GetThumbprint(certificate);

            return channelDefinition.AccessList.Thumbprints.Contains(thumbprint);
        }
    }
}

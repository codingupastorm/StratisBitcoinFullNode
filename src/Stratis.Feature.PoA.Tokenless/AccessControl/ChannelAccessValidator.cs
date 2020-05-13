using CertificateAuthority;
using Org.BouncyCastle.X509;
using Stratis.Feature.PoA.Tokenless.Networks;

namespace Stratis.Feature.PoA.Tokenless.AccessControl
{
    public interface IChannelAccessValidator
    {
        bool ValidateCertificateIsPermittedOnChannel(X509Certificate certificate, ChannelNetwork network);
    }

    public class ChannelAccessValidator : IChannelAccessValidator
    {
        public bool ValidateCertificateIsPermittedOnChannel(X509Certificate certificate, ChannelNetwork network)
        {
            // TODO: Get the up-to-date list of who is allowed on a channel, not use the initial one

            string organisation = certificate.GetOrganisation();

            if (network.InitialAccessList.Organisations.Contains(organisation))
                return true;

            string thumbprint = CaCertificatesManager.GetThumbprint(certificate);

            return network.InitialAccessList.Thumbprints.Contains(thumbprint);
        }
    }
}

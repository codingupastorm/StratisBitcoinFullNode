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
            // In future iterations we will add complexity around who is allowed on a channel here.

            return certificate.GetOrganisation() == network.Organisation;
        }
    }
}

using Org.BouncyCastle.X509;

namespace Stratis.Features.PoA
{
    public interface IClientCertificateValidator
    {
        void ConfirmCertificatePermittedOnChannel(X509Certificate certificate);
    }
}

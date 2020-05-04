using Org.BouncyCastle.X509;

namespace Stratis.Features.PoA
{
    public interface IClientCertificateValidator
    {
        void ConfirmValid(X509Certificate certificate);
    }
}

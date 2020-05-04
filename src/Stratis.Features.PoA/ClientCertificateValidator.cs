using Org.BouncyCastle.X509;

namespace Stratis.Features.PoA
{
    public interface IClientCertificateValidator
    {
        public void ConfirmValid(X509Certificate certificate);
    }
}

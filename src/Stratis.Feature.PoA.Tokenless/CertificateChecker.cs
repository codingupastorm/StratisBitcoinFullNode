using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface ICertificateChecker
    {
        bool CheckSenderCertificateHasPermission(uint160 address);
    }
    public class CertificateChecker : ICertificateChecker
    {
        public bool CheckSenderCertificateHasPermission(uint160 address)
        {
            throw new System.NotImplementedException();
        }
    }
}

using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface ICertificateCache
    {
        string GetCertificate(uint160 address);

        void SetCertificate(uint160 address, string certificate);
    }

    public class CertificateCache : ICertificateCache
    {
        private readonly DataFolder

        public string GetCertificate(uint160 address)
        {
            throw new System.NotImplementedException();
        }

        public void SetCertificate(uint160 address, string certificate)
        {
            throw new System.NotImplementedException();
        }
    }
}

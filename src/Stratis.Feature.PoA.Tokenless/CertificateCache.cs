using System.IO;
using System.Security.Cryptography.X509Certificates;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface ICertificateCache
    {
        X509Certificate2 GetCertificate(uint160 address);

        void SetCertificate(uint160 address, X509Certificate2 certificate);
    }

    public class CertificateCache : ICertificateCache
    {
        private readonly string certFolderPath;

        public CertificateCache(DataFolder dataFolder)
        {
            this.certFolderPath = dataFolder.RootPath + "/certs";
        }

        public X509Certificate2 GetCertificate(uint160 address)
        {
            string fullFileName = Path.Combine(this.certFolderPath, $"{address}.crt");

            if (!File.Exists(fullFileName))
                return null;

            return new X509Certificate2(fullFileName);
        }

        public void SetCertificate(uint160 address, X509Certificate2 certificate)
        {
            string fullFileName = Path.Combine(this.certFolderPath, $"{address}.crt");

            File.WriteAllBytes(fullFileName, certificate.GetRawCertData());
        }
    }
}

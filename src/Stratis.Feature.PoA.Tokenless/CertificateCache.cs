using System.IO;
using System.Security.Cryptography.X509Certificates;
using NBitcoin;
using Stratis.Bitcoin.Configuration;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface ICertificateCache
    {
        /// <summary>
        /// Gets the certificate for a known address if we have it stored locally on the node.
        /// </summary>
        /// <param name="address">Address to get the certficate for.</param>
        /// <returns>The certificate belonging to the sender.</returns>
        X509Certificate2 GetCertificate(uint160 address);

        /// <summary>
        /// Stores the given certificate locally on the node.
        /// </summary>
        /// <param name="address">The address that this certificate belongs to.</param>
        /// <param name="certificate">The certificate to store locally.</param>
        void SetCertificate(uint160 address, X509Certificate2 certificate);
    }

    public class CertificateCache : ICertificateCache
    {
        private readonly string certFolderPath;

        public CertificateCache(DataFolder dataFolder)
        {
            this.certFolderPath = Path.Combine(dataFolder.RootPath, "certs");

            if (!Directory.Exists(this.certFolderPath))
                Directory.CreateDirectory(this.certFolderPath);
        }

        /// <inheritdoc />
        public X509Certificate2 GetCertificate(uint160 address)
        {
            string fullFileName = Path.Combine(this.certFolderPath, $"{address}.crt");

            if (!File.Exists(fullFileName))
                return null;

            return new X509Certificate2(fullFileName);
        }

        /// <inheritdoc />
        public void SetCertificate(uint160 address, X509Certificate2 certificate)
        {
            string fullFileName = Path.Combine(this.certFolderPath, $"{address}.crt");

            File.WriteAllBytes(fullFileName, certificate.GetRawCertData());
        }
    }
}

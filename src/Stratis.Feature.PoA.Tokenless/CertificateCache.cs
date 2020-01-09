using System.IO;
using NBitcoin;
using Org.BouncyCastle.X509;
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
        X509Certificate GetCertificate(uint160 address);

        /// <summary>
        /// Stores the given certificate locally on the node.
        /// </summary>
        /// <param name="address">The address that this certificate belongs to.</param>
        /// <param name="certificate">The certificate to store locally.</param>
        void SetCertificate(uint160 address, X509Certificate certificate);
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
        public X509Certificate GetCertificate(uint160 address)
        {
            string fullFileName = Path.Combine(this.certFolderPath, $"{address}.crt");

            if (!File.Exists(fullFileName))
                return null;

            var certParser = new X509CertificateParser();

            return certParser.ReadCertificate(File.ReadAllBytes(fullFileName));
        }

        /// <inheritdoc />
        public void SetCertificate(uint160 address, X509Certificate certificate)
        {
            string fullFileName = Path.Combine(this.certFolderPath, $"{address}.crt");

            File.WriteAllBytes(fullFileName, certificate.GetEncoded());
        }
    }
}

using Newtonsoft.Json;
using Org.BouncyCastle.Pkcs;

namespace CertificateAuthority.Models
{
    /// <summary>Model that contains information related to a specific certificate.</summary>
    public class CertificateInfoModel
    {
        public int Id { get; set; }

        public string Thumbprint { get; set; }

        /// <summary>
        /// The P2PKH address corresponding to the private key of the certificate.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// The public key corresponding to the private key of the certificate.
        /// </summary>
        public string PubKey { get; set; }

        /// <summary>Certificate data encoded in DER format, converted to base64.</summary>
        public string CertificateContentDer { get; set; }

        public CertificateStatus Status { get; set; }

        public int IssuerAccountId { get; set; }

        public int RevokerAccountId { get; set; } = -1;

        public override string ToString()
        {
            return $"{nameof(this.Id)}:{this.Id},{nameof(this.Thumbprint)}:{this.Thumbprint},{nameof(this.Address)}:{this.Address},{nameof(this.PubKey)}:{this.PubKey}," +
                   $"{nameof(this.Status)}:{this.Status},{nameof(this.IssuerAccountId)}:{this.IssuerAccountId}," +
                   $"{nameof(this.RevokerAccountId)}:{this.RevokerAccountId}";
        }
    }

    public class CertificateSigningRequestModel
    {
        /// <summary>Certificate signing request in base64 format.</summary>
        public string CertificateSigningRequestContent { get; set; }

        [JsonConstructor]
        public CertificateSigningRequestModel(string base64csr)
        {
            this.CertificateSigningRequestContent = base64csr;
        }

        public CertificateSigningRequestModel(Pkcs10CertificationRequestDelaySigned request)
        {
            this.CertificateSigningRequestContent = System.Convert.ToBase64String(request.GetDerEncoded());
        }

        public override string ToString()
        {
            return $"{nameof(this.CertificateSigningRequestContent)}={this.CertificateSigningRequestContent}";
        }
    }

    public enum CertificateStatus
    {
        Good = 1,
        Revoked = 2,
        Unknown = 3
    }
}

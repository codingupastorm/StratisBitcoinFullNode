﻿namespace CertificateAuthority.Code.Models
{
    /// <summary>Model that contains information related to a specific certificate.</summary>
    public class CertificateInfoModel
    {
        public int Id { get; set; }

        public string Thumbprint { get; set; }

        /// <summary>Certificate in the following format: <c>-----BEGIN CERTIFICATE----- MIIE1jCCAr ... 7w1gjwn -----END CERTIFICATE-----</c>.</summary>
        public string CertificateContent { get; set; }

        public CertificateStatus Status { get; set; }

        public int IssuerAccountId { get; set; }

        public int RevokerAccountId { get; set; } = -1;

        public override string ToString()
        {
            return $"{nameof(this.Id)}:{this.Id},{nameof(this.Thumbprint)}:{this.Thumbprint},{nameof(this.Status)}:{this.Status}," +
                   $"{nameof(this.IssuerAccountId)}:{this.IssuerAccountId},{nameof(this.RevokerAccountId)}:{this.RevokerAccountId}";
        }
    }

    public enum CertificateStatus
    {
        Good = 1,
        Revoked = 2,
        Unknown = 3
    }
}

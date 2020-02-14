using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Org.BouncyCastle.Pkcs;

namespace CertificateAuthority.Models
{
    /// <summary>
    /// Converter used to convert <see cref="byte"/> arrays to and from JSON.
    /// </summary>
    /// <seealso cref="Newtonsoft.Json.JsonConverter" />
    public class ByteArrayConverter : JsonConverter
    {
        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(byte[]);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return Convert.FromBase64String((string)reader.Value);
        }

        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(Convert.ToBase64String((byte[])value));
        }
    }

    /// <summary>
    /// Compares two byte arrays for equality.
    /// </summary>
    public sealed class ByteArrayComparer : IEqualityComparer<byte[]>, IComparer<byte[]>
    {
        public int Compare(byte[] first, byte[] second)
        {
            int firstLen = first?.Length ?? -1;
            int secondLen = second?.Length ?? -1;
            int commonLen = Math.Min(firstLen, secondLen);

            for (int i = 0; i < commonLen; i++)
            {
                if (first[i] == second[i])
                    continue;

                return (first[i] < second[i]) ? -1 : 1;
            }

            return firstLen.CompareTo(secondLen);
        }

        public bool Equals(byte[] first, byte[] second)
        {
            return this.Compare(first, second) == 0;
        }

        public int GetHashCode(byte[] obj)
        {
            ulong hash = 17;

            foreach (byte objByte in obj)
            {
                hash = (hash << 5) - hash + objByte;
            }

            return (int)hash;
        }
    }

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
        /// The public key hash corresponding to the certificate-bearing node's transaction signing key.
        /// </summary>
        /// <remarks>This is NOT the pubkey hash of the certificate's private key.</remarks>
        [JsonConverter(typeof(ByteArrayConverter))]
        public byte[] TransactionSigningPubKeyHash { get; set; }

        /// <summary>
        /// The public key hash corresponding to the certificate-bearing node's block signing key.
        /// </summary>
        /// <remarks>This is NOT the pubkey corresponding to the certificate's private key.</remarks>
        [JsonConverter(typeof(ByteArrayConverter))]
        public byte[] BlockSigningPubKey { get; set; }

        /// <summary>Certificate data encoded in DER format, converted to base64.</summary>
        [JsonConverter(typeof(ByteArrayConverter))]
        public byte[] CertificateContentDer { get; set; }

        public CertificateStatus Status { get; set; }

        /// <summary>The ID of the account that this certificate belongs to.</summary>
        public int AccountId { get; set; }

        public int RevokerAccountId { get; set; } = -1;

        public override string ToString()
        {
            return $"{nameof(this.Id)}:{this.Id},{nameof(this.Thumbprint)}:{this.Thumbprint},{nameof(this.Address)}:{this.Address},{nameof(this.TransactionSigningPubKeyHash)}:{this.TransactionSigningPubKeyHash}," +
                   $"{nameof(this.BlockSigningPubKey)}:{this.BlockSigningPubKey},{nameof(this.Status)}:{this.Status},{nameof(this.AccountId)}:{this.AccountId}," +
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

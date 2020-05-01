using System.Collections.Generic;
using CertificateAuthority.Models;
using NBitcoin;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;

namespace Stratis.Features.PoA.ProtocolEncryption
{
    public interface ICertificatesManager
    {
        /// <summary>Root certificate of the certificate authority for the current network.</summary>
        X509Certificate AuthorityCertificate { get; }

        /// <summary>Client certificate that is used to establish connections with other peers.</summary>
        X509Certificate ClientCertificate { get; }

        /// <summary>The private key associated with the loaded client certificate. Intended to be used for TLS communication only.</summary>
        AsymmetricKeyParameter ClientCertificatePrivateKey { get; }

        /// <summary>Loads client and authority certificates and validates them.</summary>
        /// <exception cref="CertificateConfigurationException">Thrown in case required certificates are not found or are not valid.</exception>
        void Initialize();

        bool HaveAccount();

        bool LoadAuthorityCertificate(bool requireAccountId = true);

        bool LoadClientCertificate();

        List<PubKey> GetCertificatePublicKeys();
    }
}

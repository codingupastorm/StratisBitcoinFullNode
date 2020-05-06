using System.Collections.Generic;
using System.Linq;
using CertificateAuthority;
using CertificateAuthority.Models;
using MembershipServices;
using NBitcoin;
using NBitcoin.Crypto;
using Org.BouncyCastle.X509;
using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public interface IEndorsementSignatureValidator
    {
        bool Validate(IEnumerable<Endorsement> endorsements, byte[] data);
        bool Validate(Endorsement endorsement, byte[] data);
    }

    /// <summary>
    /// Validates endorsement signatures against a payload.
    /// </summary>
    public class EndorsementSignatureSignatureValidator : IEndorsementSignatureValidator
    {
        private readonly IMembershipServicesDirectory membershipServices;
        private readonly ICertificatePermissionsChecker permissionsChecker;

        public EndorsementSignatureSignatureValidator(IMembershipServicesDirectory membershipServices, ICertificatePermissionsChecker permissionsChecker)
        {
            this.membershipServices = membershipServices;
            this.permissionsChecker = permissionsChecker;
        }

        public bool Validate(IEnumerable<Endorsement> endorsements, byte[] data)
        {
            return endorsements.All(e => this.Validate(e, data));
        }
        
        public bool Validate(Endorsement endorsement, byte[] data)
        {
            var pubKey = new PubKey(endorsement.PubKey);
            X509Certificate cert = this.membershipServices.GetCertificateForTransactionSigningPubKeyHash(pubKey.Hash.ToBytes());

            if (cert == null)
            {
                return false;
            }

            var hash = new uint256(HashHelper.Keccak256(data));

            var signature = new ECDSASignature(endorsement.Signature);

            var thumbprint = CaCertificatesManager.GetThumbprint(cert);

            return this.permissionsChecker.CheckSignature(thumbprint, signature, pubKey, hash);
        }
    }
}
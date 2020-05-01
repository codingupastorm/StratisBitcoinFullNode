using System.Collections.Generic;
using System.Linq;
using CertificateAuthority;
using CertificateAuthority.Models;
using NBitcoin;
using NBitcoin.Crypto;
using Org.BouncyCastle.X509;
using Stratis.Features.PoA.ProtocolEncryption;
using Stratis.SmartContracts.Core.Hashing;
using ByteArrayComparer = Stratis.Bitcoin.Utilities.ByteArrayComparer;

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
        private readonly ICertificatesManager certificatesManager;
        private readonly ICertificatePermissionsChecker permissionsChecker;
        private readonly ByteArrayComparer byteArrayComparer;

        public EndorsementSignatureSignatureValidator(ICertificatesManager certificatesManager, ICertificatePermissionsChecker permissionsChecker)
        {
            this.certificatesManager = certificatesManager;
            this.permissionsChecker = permissionsChecker;
            this.byteArrayComparer = new ByteArrayComparer();
        }

        public bool Validate(IEnumerable<Endorsement> endorsements, byte[] data)
        {
            return endorsements.All(e => this.Validate(e, data));
        }
        
        public bool Validate(Endorsement endorsement, byte[] data)
        {
            var pubKey = new PubKey(endorsement.PubKey);

            CertificateInfoModel certificateInfoModel = this.certificatesManager.GetAllCertificates()
                .FirstOrDefault(c =>
                    this.byteArrayComparer.Equals(c.TransactionSigningPubKeyHash, pubKey.Hash.ToBytes()));

            if (certificateInfoModel == null)
            {
                return false;
            }

            var hash = new uint256(HashHelper.Keccak256(data));

            var signature = new ECDSASignature(endorsement.Signature);

            return this.permissionsChecker.CheckSignature(certificateInfoModel.Thumbprint, signature, pubKey, hash);
        }
    }
}
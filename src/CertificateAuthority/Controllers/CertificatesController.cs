using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using CertificateAuthority.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using NLog;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;

namespace CertificateAuthority.Controllers
{
    [Produces("application/json")]
    [Route("api/certificates")]
    [ApiController]
    public class CertificatesController : LoggedController
    {
        private readonly CaCertificatesManager caCertificateManager;

        public CertificatesController(CaCertificatesManager caCertificateManager) : base(LogManager.GetCurrentClassLogger())
        {
            this.caCertificateManager = caCertificateManager;
        }

        [HttpPost("initialize_ca")]
        [ProducesResponseType(typeof(bool), 200)]
        public IActionResult InitializeCertificateAuthority([FromBody]CredentialsModelWithMnemonicModel model)
        {
            this.LogEntry(model);

            var data = new CredentialsAccessWithModel<CredentialsModelWithMnemonicModel>(model, AccountAccessFlags.InitializeCertificateAuthority);

            try
            {
                var certificate = this.caCertificateManager.InitializeCertificateAuthority(data.Model.Mnemonic, data.Model.MnemonicPassword, data.Model.CoinType, data.Model.AddressPrefix);

                return this.Json(this.LogExit(certificate));
            }
            catch (InvalidCredentialsException)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status403Forbidden));
            }
            catch (Exception ex)
            {
                return this.LogErrorExit(BadRequest(ex));
            }
        }

        /// <summary>
        /// Sets certificate status with the provided thumbprint to <see cref="CertificateStatus.Revoked"/>
        /// if certificate was found and it's status is <see cref="CertificateStatus.Good"/>.
        /// RevokeCertificates access level is required.
        /// </summary>
        [HttpPost("revoke_certificate")]
        [ProducesResponseType(typeof(bool), 200)]
        public IActionResult RevokeCertificate([FromBody]CredentialsModelWithThumbprintModel model)
        {
            this.LogEntry(model);

            var data = new CredentialsAccessWithModel<CredentialsModelWithThumbprintModel>(model, AccountAccessFlags.RevokeCertificates);

            try
            {
                var res = this.caCertificateManager.RevokeCertificate(data);

                return this.Json(this.LogExit(res));
            }
            catch (InvalidCredentialsException)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status403Forbidden));
            }
            catch (Exception ex)
            {
                return this.LogErrorExit(BadRequest(ex));
            }
        }

        [HttpPost("get_ca_certificate")]
        [ProducesResponseType(typeof(CertificateInfoModel), 200)]
        public IActionResult GetCaCertificate([FromBody]CredentialsModel model)
        {
            this.LogEntry(model);

            var data = new CredentialsAccessModel(model.AccountId, model.Password, AccountAccessFlags.AccessAnyCertificate);

            try
            {
                CertificateInfoModel res = this.caCertificateManager.GetCaCertificate(data);

                if (res == null)
                    return this.LogErrorExit(StatusCode(StatusCodes.Status404NotFound));

                return this.Json(this.LogExit(res));
            }
            catch (InvalidCredentialsException)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status403Forbidden));
            }
            catch (Exception ex)
            {
                return this.LogErrorExit(BadRequest(ex));
            }
        }

        /// <summary>Finds issued certificate by thumbprint and returns it or null if it wasn't found. AccessAnyCertificate access level is required.</summary>
        [HttpPost("get_certificate_for_thumbprint")]
        [ProducesResponseType(typeof(CertificateInfoModel), 200)]
        public IActionResult GetCertificateByThumbprint([FromBody]CredentialsModelWithThumbprintModel model)
        {
            this.LogEntry(model);

            var data = new CredentialsAccessWithModel<CredentialsModelWithThumbprintModel>(model, AccountAccessFlags.AccessAnyCertificate);

            try
            {
                CertificateInfoModel certificate = this.caCertificateManager.GetCertificateByThumbprint(data);

                if (certificate == null)
                    return this.LogErrorExit(StatusCode(StatusCodes.Status404NotFound));

                return this.Json(this.LogExit(certificate));
            }
            catch (InvalidCredentialsException)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status403Forbidden));
            }
            catch (Exception ex)
            {
                return this.LogErrorExit(BadRequest(ex));
            }
        }

        /// <summary>Finds issued certificate by P2PKH address and returns it or null if it wasn't found. AccessAnyCertificate access level is required.</summary>
        [HttpPost("get_certificate_for_address")]
        [ProducesResponseType(typeof(CertificateInfoModel), 200)]
        public IActionResult GetCertificateByAddress([FromBody]CredentialsModelWithAddressModel model)
        {
            this.LogEntry(model);

            var data = new CredentialsAccessWithModel<CredentialsModelWithAddressModel>(model, AccountAccessFlags.AccessAnyCertificate);

            try
            {
                CertificateInfoModel certificate = this.caCertificateManager.GetCertificateByAddress(data);

                if (certificate == null)
                    return StatusCode(StatusCodes.Status404NotFound);

                return this.Json(this.LogExit(certificate));
            }
            catch (InvalidCredentialsException)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status403Forbidden));
            }
        }

        /// <summary>Finds issued certificate by pubkey and returns it or null if it wasn't found. AccessAnyCertificate access level is required.</summary>
        [HttpPost("get_certificate_for_pubkey_hash")]
        [ProducesResponseType(typeof(CertificateInfoModel), 200)]
        public IActionResult GetCertificateForPubKeyHash([FromBody]CredentialsModelWithPubKeyHashModel model)
        {
            this.LogEntry(model);

            var data = new CredentialsAccessWithModel<CredentialsModelWithPubKeyHashModel>(model, AccountAccessFlags.AccessAnyCertificate);

            try
            {
                CertificateInfoModel certificate = this.caCertificateManager.GetCertificateByPubKeyHash(data);

                if (certificate == null)
                    return this.LogErrorExit(StatusCode(StatusCodes.Status404NotFound));

                return this.Json(this.LogExit(certificate));
            }
            catch (InvalidCredentialsException)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status403Forbidden));
            }
            catch (Exception ex)
            {
                return this.LogErrorExit(BadRequest(ex));
            }
        }

        /// <summary>Provides collection of all issued certificates. AccessAnyCertificate access level is required.</summary>
        /// <response code="201">Collection of <see cref="CertificateInfoModel"/> instances."/>.</response>
        [HttpPost("get_all_certificates")]
        [ProducesResponseType(typeof(List<CertificateInfoModel>), 200)]
        public IActionResult GetAllCertificates([FromBody]CredentialsModel model)
        {
            this.LogEntry(model);

            var data = new CredentialsAccessModel(model.AccountId, model.Password, AccountAccessFlags.AccessAnyCertificate);

            try
            {
                return this.Json(this.LogExit(this.caCertificateManager.GetAllCertificates(data)));
            }
            catch (InvalidCredentialsException)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status403Forbidden));
            }
            catch (Exception ex)
            {
                return this.LogErrorExit(BadRequest(ex));
            }
        }

        /// <summary>Creates a template certificate request without a signature. IssueCertificates access level is required.</summary>
        /// <response code="200">Instance of <see cref="CertificateSigningRequestModel"/>.</response>
        [HttpPost("generate_certificate_signing_request")]
        [ProducesResponseType(typeof(CertificateSigningRequestModel), 200)]
        public async Task<IActionResult> GenerateCertificateSigningRequestAsync([FromBody]GenerateCertificateSigningRequestModel model)
        {
            this.LogEntry(model);

            try
            {
                var data = new CredentialsAccessWithModel<GenerateCertificateSigningRequestModel>(model, AccountAccessFlags.IssueCertificates);

                byte[] oid141 = Encoding.UTF8.GetBytes(data.Model.Address);
                byte[] oid142 = Convert.FromBase64String(data.Model.TransactionSigningPubKeyHash);
                byte[] oid144 = Convert.FromBase64String(data.Model.BlockSigningPubKey);

                var extensionData = new Dictionary<string, byte[]>
                {
                    {CaCertificatesManager.P2pkhExtensionOid, oid141},
                    {CaCertificatesManager.TransactionSigningPubKeyHashExtensionOid, oid142},
                    {CaCertificatesManager.BlockSigningPubKeyExtensionOid, oid144}
                };

                byte[] pubKeyBytes = Convert.FromBase64String(data.Model.PubKey);
                X9ECParameters ecdsaCurve = ECNamedCurveTable.GetByName("secp256k1");
                var ecdsaDomainParams = new ECDomainParameters(ecdsaCurve.Curve, ecdsaCurve.G, ecdsaCurve.N, ecdsaCurve.H, ecdsaCurve.GetSeed());
                var q = new X9ECPoint(ecdsaCurve.Curve, pubKeyBytes);

                AsymmetricKeyParameter publicKey = new ECPublicKeyParameters(q.Point, ecdsaDomainParams);

                // The subject DN comes from the original information supplied by the user upon account creation.
                string subjectName = this.caCertificateManager.GetClientCertificateSubjectDistinguishedName(data);

                string[] subjectAlternativeNames = this.caCertificateManager.GetClientCertificateSubjectAlternativeNames(data);

                Pkcs10CertificationRequestDelaySigned unsignedCsr = CaCertificatesManager.CreatedUnsignedCertificateSigningRequest(subjectName, publicKey, subjectAlternativeNames, extensionData);

                // Important workaround - fill in a dummy signature so that when the CSR is reconstituted on the far side, the decoding does not fail with DerNull errors.
                unsignedCsr.SignRequest(new byte[] { });

                var csrModel = new CertificateSigningRequestModel(unsignedCsr);

                return this.Json(this.LogExit(csrModel));
            }
            catch (InvalidCredentialsException)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status403Forbidden));
            }
            catch (Exception ex)
            {
                return this.LogErrorExit(BadRequest(ex));
            }
        }

        /// <summary>Issues a new certificate using provided certificate request. IssueCertificates access level is required.</summary>
        /// <response code="201">Instance of <see cref="CertificateInfoModel"/>.</response>
        [HttpPost("issue_certificate_using_request_file")]
        [ProducesResponseType(typeof(CertificateInfoModel), 200)]
        public async Task<IActionResult> IssueCertificate_UsingRequestFileAsync([FromBody]IssueCertificateFromRequestModel model)
        {
            this.LogEntry(model);

            var data = new CredentialsAccessWithModel<IssueCertificateFromRequestModel>(model, AccountAccessFlags.IssueCertificates);

            try
            {
                CertificateInfoModel infoModel = await this.caCertificateManager.IssueCertificateAsync(data);
                return this.Json(this.LogExit(infoModel));
            }
            catch (InvalidCredentialsException)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status403Forbidden));
            }
            catch (Exception ex)
            {
                return this.LogErrorExit(BadRequest(ex));
            }
        }

        /// <summary>Issues a new certificate using provided certificate request string. IssueCertificates access level is required.</summary>
        /// <response code="201">Instance of <see cref="CertificateInfoModel"/>.</response>
        [HttpPost("issue_certificate_using_request_string")]
        [ProducesResponseType(typeof(CertificateInfoModel), 200)]
        public async Task<IActionResult> IssueCertificate_UsingRequestStringAsync([FromBody]IssueCertificateFromFileContentsModel model)
        {
            this.LogEntry(model);

            var data = new CredentialsAccessWithModel<IssueCertificateFromFileContentsModel>(model, AccountAccessFlags.IssueCertificates);

            try
            {
                if (data.Model.CertificateRequestFileContents.Length == 0)
                    return BadRequest();

                if (string.IsNullOrEmpty(data.Model.CertificateRequestFileContents))
                    return BadRequest();

                CertificateInfoModel infoModel = await this.caCertificateManager.IssueCertificateAsync(data);

                return this.Json(this.LogExit(infoModel));
            }
            catch (InvalidCredentialsException)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status403Forbidden));
            }
            catch (Exception ex)
            {
                return this.LogErrorExit(BadRequest(ex));
            }
        }

        /// <summary>
        /// Gets status of the certificate with the provided thumbprint or
        /// returns <see cref="CertificateStatus.Unknown"/> if certificate wasn't found.
        /// </summary>
        /// <response code="200">Certificate status string.</response>
        [HttpPost]
        [Route("get_certificate_status")]
        [ProducesResponseType(typeof(string), 200)]
        public IActionResult GetCertificateStatus([FromBody]GetCertificateStatusModel model)
        {
            this.LogEntry(model);

            try
            {
                CertificateStatus status = this.caCertificateManager.GetCertificateStatusByThumbprint(model.Thumbprint);

                if (model.AsString)
                    return this.Json(this.LogExit(status.ToString()));

                return this.Json(this.LogExit((int)status).ToString());
            }
            catch (Exception ex)
            {
                return this.LogErrorExit(BadRequest(ex));
            }
        }

        /// <summary>Returns a collection of thumbprints of revoked certificates.</summary>
        /// <response code="200">Collection of <see cref="string"/>.</response>
        [HttpPost]
        [Route("get_revoked_certificates")]
        [ProducesResponseType(typeof(ICollection<string>), 200)]
        public IActionResult GetRevokedCertificates()
        {
            this.LogEntry();

            return this.Json(this.LogExit(this.caCertificateManager.GetRevokedCertificates()));
        }

        /// <summary>Returns the public key value (oid142) for all non-revoked certificates.</summary>
        /// <response code="200">Collection of <see cref="PubKey"/>.</response>
        [HttpPost]
        [Route("get_certificate_public_keys")]
        [ProducesResponseType(typeof(ICollection<string>), 200)]
        public IActionResult GetCertificatePublicKeys()
        {
            // TODO: This and presumably the other methods here should be checking credentials!!

            this.LogEntry();

            return this.Json(this.LogExit(this.caCertificateManager.GetCertificatePublicKeys()));
        }
    }
}
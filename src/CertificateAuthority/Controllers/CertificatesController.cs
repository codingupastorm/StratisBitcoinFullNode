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
    [Route("api/certificates")]
    [ApiController]
    public class CertificatesController : Controller
    {
        private readonly CaCertificatesManager caCertificateManager;
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        public CertificatesController(CaCertificatesManager caCertificateManager)
        {
            this.caCertificateManager = caCertificateManager;
        }

        [HttpPost("initialize_ca")]
        [ProducesResponseType(typeof(bool), 200)]
        public ActionResult<bool> InitializeCertificateAuthority([FromBody]CredentialsModelWithMnemonicModel model)
        {
            var data = new CredentialsAccessWithModel<CredentialsModelWithMnemonicModel>(model, AccountAccessFlags.InitializeCertificateAuthority);

            try
            {
                return this.caCertificateManager.InitializeCertificateAuthority(data.Model.Mnemonic, data.Model.MnemonicPassword);
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Sets certificate status with the provided thumbprint to <see cref="CertificateStatus.Revoked"/>
        /// if certificate was found and it's status is <see cref="CertificateStatus.Good"/>.
        /// RevokeCertificates access level is required.
        /// </summary>
        [HttpPost("revoke_certificate")]
        [ProducesResponseType(typeof(bool), 200)]
        public ActionResult<bool> RevokeCertificate([FromBody]CredentialsModelWithThumbprintModel model)
        {
            var data = new CredentialsAccessWithModel<CredentialsModelWithThumbprintModel>(model, AccountAccessFlags.RevokeCertificates);

            try
            {
                return this.caCertificateManager.RevokeCertificate(data);
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        [HttpPost("get_ca_certificate")]
        [ProducesResponseType(typeof(CertificateInfoModel), 200)]
        public ActionResult<CertificateInfoModel> GetCaCertificate([FromBody]CredentialsModel model)
        {
            var data = new CredentialsAccessModel(model.AccountId, model.Password, AccountAccessFlags.AccessAnyCertificate);

            try
            {
                CertificateInfoModel certificate = this.caCertificateManager.GetCaCertificate(data);

                if (certificate == null)
                    return StatusCode(StatusCodes.Status404NotFound);

                return certificate;
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>Finds issued certificate by thumbprint and returns it or null if it wasn't found. AccessAnyCertificate access level is required.</summary>
        [HttpPost("get_certificate_for_thumbprint")]
        [ProducesResponseType(typeof(CertificateInfoModel), 200)]
        public ActionResult<CertificateInfoModel> GetCertificateByThumbprint([FromBody]CredentialsModelWithThumbprintModel model)
        {
            var data = new CredentialsAccessWithModel<CredentialsModelWithThumbprintModel>(model, AccountAccessFlags.AccessAnyCertificate);

            try
            {
                CertificateInfoModel certificate = this.caCertificateManager.GetCertificateByThumbprint(data);

                if (certificate == null)
                    return StatusCode(StatusCodes.Status404NotFound);

                return certificate;
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>Finds issued certificate by P2PKH address and returns it or null if it wasn't found. AccessAnyCertificate access level is required.</summary>
        [HttpPost("get_certificate_for_address")]
        [ProducesResponseType(typeof(CertificateInfoModel), 200)]
        public ActionResult<CertificateInfoModel> GetCertificateByAddress([FromBody]CredentialsModelWithAddressModel model)
        {
            var data = new CredentialsAccessWithModel<CredentialsModelWithAddressModel>(model, AccountAccessFlags.AccessAnyCertificate);

            try
            {
                CertificateInfoModel certificate = this.caCertificateManager.GetCertificateByAddress(data);

                if (certificate == null)
                    return StatusCode(StatusCodes.Status404NotFound);

                return certificate;
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>Provides collection of all issued certificates. AccessAnyCertificate access level is required.</summary>
        /// <response code="201">Collection of <see cref="CertificateInfoModel"/> instances."/>.</response>
        [HttpPost("get_all_certificates")]
        [ProducesResponseType(typeof(List<CertificateInfoModel>), 200)]
        public ActionResult<List<CertificateInfoModel>> GetAllCertificates([FromBody]CredentialsModel model)
        {
            var data = new CredentialsAccessModel(model.AccountId, model.Password, AccountAccessFlags.AccessAnyCertificate);

            try
            {
                return this.caCertificateManager.GetAllCertificates(data);
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>Creates a template certificate request without a signature. IssueCertificates access level is required.</summary>
        /// <response code="200">Instance of <see cref="CertificateSigningRequestModel"/>.</response>
        [HttpPost("generate_certificate_signing_request")]
        [ProducesResponseType(typeof(CertificateSigningRequestModel), 200)]
        public async Task<ActionResult<CertificateSigningRequestModel>> GenerateCertificateSigningRequestAsync([FromBody]GenerateCertificateSigningRequestModel model)
        {
            var data = new CredentialsAccessWithModel<GenerateCertificateSigningRequestModel>(model, AccountAccessFlags.IssueCertificates);

            byte[] oid141 = Encoding.UTF8.GetBytes(data.Model.Address);

            byte[] pubKeyBytes = Convert.FromBase64String(data.Model.PubKey);

            X9ECParameters ecdsaCurve = ECNamedCurveTable.GetByName("secp256k1");
            var ecdsaDomainParams = new ECDomainParameters(ecdsaCurve.Curve, ecdsaCurve.G, ecdsaCurve.N, ecdsaCurve.H, ecdsaCurve.GetSeed());
            var q = new X9ECPoint(ecdsaCurve.Curve, pubKeyBytes);

            AsymmetricKeyParameter publicKey = new ECPublicKeyParameters(q.Point, ecdsaDomainParams);

            try
            {
                string subjectName = $"CN={data.Model.Address}";

                Pkcs10CertificationRequestDelaySigned unsignedCsr = CaCertificatesManager.CreatedUnsignedCertificateSigningRequest(subjectName, publicKey, new string[0], oid141, pubKeyBytes);

                // Important workaround - fill in a dummy signature so that when the CSR is reconstituted on the far side, the decoding does not fail with DerNull errors.
                unsignedCsr.SignRequest(new byte[] { });

                var csrModel = new CertificateSigningRequestModel(unsignedCsr);

                return csrModel;
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>Issues a new certificate using provided certificate request. IssueCertificates access level is required.</summary>
        /// <response code="201">Instance of <see cref="CertificateInfoModel"/>.</response>
        [HttpPost("issue_certificate_using_request_file")]
        [ProducesResponseType(typeof(CertificateInfoModel), 200)]
        public async Task<ActionResult<CertificateInfoModel>> IssueCertificate_UsingRequestFileAsync([FromBody]IssueCertificateFromRequestModel model)
        {
            var data = new CredentialsAccessWithModel<IssueCertificateFromRequestModel>(model, AccountAccessFlags.IssueCertificates);

            try
            {
                CertificateInfoModel infoModel = await this.caCertificateManager.IssueCertificateAsync(data);
                return infoModel;
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (Exception)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>Issues a new certificate using provided certificate request string. IssueCertificates access level is required.</summary>
        /// <response code="201">Instance of <see cref="CertificateInfoModel"/>.</response>
        [HttpPost("issue_certificate_using_request_string")]
        [ProducesResponseType(typeof(CertificateInfoModel), 200)]
        public async Task<ActionResult<CertificateInfoModel>> IssueCertificate_UsingRequestStringAsync([FromBody]IssueCertificateFromFileContentsModel model)
        {
            var data = new CredentialsAccessWithModel<IssueCertificateFromFileContentsModel>(model, AccountAccessFlags.IssueCertificates);

            try
            {
                if (data.Model.CertificateRequestFileContents.Length == 0)
                    return BadRequest();

                if (string.IsNullOrEmpty(data.Model.CertificateRequestFileContents))
                    return BadRequest();

                CertificateInfoModel infoModel = await this.caCertificateManager.IssueCertificateAsync(data);

                return infoModel;
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (Exception e)
            {
                this.logger.Error(e);

                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets status of the certificate with the provided thumbprint or
        /// returns <see cref="CertificateStatus.Unknown"/> if certificate wasn't found.
        /// </summary>
        /// <response code="200">Certificate status string.</response>
        [HttpGet]
        [Route("get_certificate_status")]
        [ProducesResponseType(typeof(string), 200)]
        public ActionResult<string> GetCertificateStatus([FromQuery]GetCertificateStatusModel model)
        {
            CertificateStatus status = this.caCertificateManager.GetCertificateStatusByThumbprint(model.Thumbprint);

            if (model.AsString)
                return status.ToString();

            return ((int)status).ToString();
        }

        /// <summary>Returns a collection of thumbprints of revoked certificates.</summary>
        /// <response code="200">Collection of <see cref="string"/>.</response>
        [HttpGet]
        [Route("get_revoked_certificates")]
        [ProducesResponseType(typeof(ICollection<string>), 200)]
        public ActionResult<ICollection<string>> GetRevokedCertificates()
        {
            return this.caCertificateManager.GetRevokedCertificates();
        }

        /// <summary>Returns the public key value (oid142) for all non-revoked certificates.</summary>
        /// <response code="200">Collection of <see cref="string"/> with the hex representations of the public keys.</response>
        [HttpGet]
        [Route("get_certificate_public_keys")]
        [ProducesResponseType(typeof(ICollection<PubKey>), 200)]
        public ActionResult<ICollection<PubKey>> GetCertificatePublicKeys()
        {
            return this.caCertificateManager.GetCertificatePublicKeys();
        }
    }
}
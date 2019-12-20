using CertificateAuthority.Code.Models;
using Microsoft.AspNetCore.Mvc;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace CertificateAuthority.Code.Controllers
{
    [Route("api/certificates")]
    [ApiController]
    public class CertificatesController : Controller
    {
        private readonly CertificatesManager certificateManager;
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        public CertificatesController(CertificatesManager certificateManager)
        {
            this.certificateManager = certificateManager;
        }

        [HttpPost("initialize_ca")]
        [ProducesResponseType(typeof(bool), 200)]
        public ActionResult<bool> InitializeCertificateAuthority([FromBody]CredentialsModelWithMnemonicModel model)
        {
            var data = new CredentialsAccessWithModel<CredentialsModelWithMnemonicModel>(model, AccountAccessFlags.InitializeCertificateAuthority);

            try
            {
                return this.certificateManager.InitializeCertificateAuthority(data.Model.Mnemonic, data.Model.MnemonicPassword);
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
                return this.certificateManager.RevokeCertificate(data);
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>Finds issued certificate by thumbprint and returns it or null if it wasn't found. AccessAnyCertificate access level is required.</summary>
        [HttpPost("get_certificate")]
        [ProducesResponseType(typeof(CertificateInfoModel), 200)]
        public ActionResult<CertificateInfoModel> GetCertificateByThumbprint([FromBody]CredentialsModelWithThumbprintModel model)
        {
            var data = new CredentialsAccessWithModel<CredentialsModelWithThumbprintModel>(model, AccountAccessFlags.AccessAnyCertificate);

            try
            {
                CertificateInfoModel certificate = this.certificateManager.GetCertificateByThumbprint(data);

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
                CertificateInfoModel certificate = this.certificateManager.GetCertificateByAddress(data);

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
                return this.certificateManager.GetAllCertificates(data);
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
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
                CertificateInfoModel infoModel = await this.certificateManager.IssueCertificateAsync(data);
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

                CertificateInfoModel infoModel = await this.certificateManager.IssueCertificateAsync(data);

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
        /// Get's status of the certificate with the provided thumbprint or
        /// returns <see cref="CertificateStatus.Unknown"/> if certificate wasn't found.
        /// </summary>
        /// <response code="201">Certificate status string.</response>
        [HttpGet]
        [Route("get_certificate_status")]
        [ProducesResponseType(typeof(string), 200)]
        public ActionResult<string> GetCertificateStatus([FromQuery]GetCertificateStatusModel model)
        {
            CertificateStatus status = this.certificateManager.GetCertificateStatusByThumbprint(model.Thumbprint);

            if (model.AsString)
                return status.ToString();

            return ((int)status).ToString();
        }

        /// <summary>Provides a collection of thumbprints of revoked certificates.</summary>
        /// <response code="201">Collection of <see cref="string"/>.</response>
        [HttpGet]
        [Route("get_revoked_certificates")]
        [ProducesResponseType(typeof(ICollection<string>), 200)]
        public ActionResult<ICollection<string>> GetRevokedCertificates()
        {
            return certificateManager.GetRevokedCertificates();
        }
    }
}
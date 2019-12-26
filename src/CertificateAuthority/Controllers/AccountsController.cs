using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using CertificateAuthority.Database;
using CertificateAuthority.Models;
using Microsoft.AspNetCore.Http;

namespace CertificateAuthority.Controllers
{
    [Route("api/accounts")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly DataCacheLayer repository;

        public AccountsController(DataCacheLayer repository)
        {
            this.repository = repository;
        }

        /// <summary>Provides account information of the account with id specified. AccessAccountInfo access level required.</summary>
        /// <response code="201">Instance of <see cref="AccountInfo"/>.</response>
        [HttpPost("get_account_info_by_id")]
        [ProducesResponseType(typeof(AccountInfo), 200)]
        public ActionResult<AccountInfo> GetAccountInfoById([FromBody]CredentialsModelWithTargetId model)
        {
            var credentials = new CredentialsAccessWithModel<CredentialsModelWithTargetId>(model, AccountAccessFlags.AccessAccountInfo);

            try
            {
                return this.repository.GetAccountInfoById(credentials);
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>Provides account information of the account with id specified. AccessAccountInfo access level required.</summary>
        /// <response code="201">Collection of <see cref="AccountModel"/> instances.</response>
        [HttpPost("list_accounts")]
        [ProducesResponseType(typeof(List<AccountModel>), 200)]
        public ActionResult<List<AccountModel>> GetAllAccounts([FromBody]CredentialsModel model)
        {
            var credentials = new CredentialsAccessModel(model.AccountId, model.Password, AccountAccessFlags.AccessAccountInfo);

            try
            {
                return this.repository.GetAllAccounts(credentials);
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>Creates new account.</summary>
        /// <response code="201">Account id as integer. CreateAccounts access level is required.</response>
        [HttpPost("create_account")]
        [ProducesResponseType(typeof(int), 200)]
        public ActionResult<int> CreateAccount([FromBody]CreateAccount model)
        {
            var credentials = new CredentialsAccessWithModel<CreateAccount>(model, AccountAccessFlags.CreateAccounts);

            try
            {
                return this.repository.CreateAccount(credentials);
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (Exception)
            {
                // TODO: This is generally the case where an account is being created with a higher access level than the creator. Create a distinct exception type for this scenario?
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>Provides collection of all certificates issued by account with specified id. AccessAnyCertificate access level is required.</summary>
        /// <response code="201">Collection of <see cref="CertificateInfoModel"/> instances.</response>
        [HttpPost("get_certificates_issued_by_account_id")]
        [ProducesResponseType(typeof(List<CertificateInfoModel>), 200)]
        public ActionResult<List<CertificateInfoModel>> GetCertificatesIssuedByAccountId([FromBody]CredentialsModelWithTargetId model)
        {
            var credentials = new CredentialsAccessWithModel<CredentialsModelWithTargetId>(model, AccountAccessFlags.AccessAnyCertificate);

            try
            {
                return this.repository.GetCertificatesIssuedByAccountId(credentials);
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>Deletes existing account with id specified. DeleteAccounts access level is required. Can't delete Admin.</summary>
        [HttpPost("delete_account_by_account_id")]
        public ActionResult DeleteAccountByAccountId([FromBody]CredentialsModelWithTargetId model)
        {
            var credentials = new CredentialsAccessWithModel<CredentialsModelWithTargetId>(model, AccountAccessFlags.DeleteAccounts);

            try
            {
                this.repository.DeleteAccount(credentials);

                return this.Ok();
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (Exception)
            {
                // TODO: Add distinct exception type for attempting to delete the admin account?
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>
        /// Sets access level of a specified account to a given value.
        /// You can't change your own or Admin's access level. You can't set account's access level to be higher than yours.
        /// ChangeAccountAccessLevel access level is required.
        /// </summary>
        [HttpPost("change_account_access_level")]
        public ActionResult ChangeAccountAccessLevel([FromBody]ChangeAccountAccessLevel model)
        {
            var credentials = new CredentialsAccessWithModel<ChangeAccountAccessLevel>(model, AccountAccessFlags.ChangeAccountAccessLevel);

            try
            {
                this.repository.ChangeAccountAccessLevel(credentials);

                return this.Ok();
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (Exception)
            {
                // TODO: Add distinct exception type for attempting to change access level?
                return StatusCode(StatusCodes.Status403Forbidden);
            }
        }
    }
}
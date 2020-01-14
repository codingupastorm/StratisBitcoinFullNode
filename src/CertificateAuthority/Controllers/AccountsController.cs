﻿using System;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using CertificateAuthority.Database;
using CertificateAuthority.Models;
using Microsoft.AspNetCore.Http;
using NLog;

namespace CertificateAuthority.Controllers
{
    [Produces("application/json")]
    [Route("api/accounts")]
    [ApiController]
    public class AccountsController : Controller
    {
        private readonly DataCacheLayer repository;
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        public AccountsController(DataCacheLayer repository)
        {
            this.repository = repository;
        }

        /// <summary>Provides account information of the account with id specified. AccessAccountInfo access level required.</summary>
        /// <response code="201">Instance of <see cref="AccountInfo"/>.</response>
        [HttpPost("get_account_info_by_id")]
        [ProducesResponseType(typeof(AccountInfo), 200)]
        public IActionResult GetAccountInfoById([FromBody]CredentialsModelWithTargetId model)
        {
            var credentials = new CredentialsAccessWithModel<CredentialsModelWithTargetId>(model, AccountAccessFlags.AccessAccountInfo);

            try
            {
                return this.Json(this.repository.GetAccountInfoById(credentials));
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex);

                return BadRequest(ex);
            }
        }

        /// <summary>Provides account information of the account with id specified. AccessAccountInfo access level required.</summary>
        /// <response code="201">Collection of <see cref="AccountModel"/> instances.</response>
        [HttpPost("list_accounts")]
        [ProducesResponseType(typeof(List<AccountModel>), 200)]
        public IActionResult GetAllAccounts([FromBody]CredentialsModel model)
        {
            var credentials = new CredentialsAccessModel(model.AccountId, model.Password, AccountAccessFlags.AccessAccountInfo);

            try
            {
                return this.Json(this.repository.GetAllAccounts(credentials));
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex);

                return BadRequest(ex);
            }
        }

        /// <summary>Creates new account.</summary>
        /// <response code="201">Account id as integer. CreateAccounts access level is required.</response>
        [HttpPost("create_account")]
        [ProducesResponseType(typeof(int), 200)]
        public IActionResult CreateAccount([FromBody]CreateAccount model)
        {
            var credentials = new CredentialsAccessWithModel<CreateAccount>(model, AccountAccessFlags.CreateAccounts);

            try
            {
                return this.Json(this.repository.CreateAccount(credentials));
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex);

                return BadRequest(ex);
            }
        }

        /// <summary>Provides collection of all certificates issued by account with specified id. AccessAnyCertificate access level is required.</summary>
        /// <response code="201">Collection of <see cref="CertificateInfoModel"/> instances.</response>
        [HttpPost("get_certificates_issued_by_account_id")]
        [ProducesResponseType(typeof(List<CertificateInfoModel>), 200)]
        public IActionResult GetCertificatesIssuedByAccountId([FromBody]CredentialsModelWithTargetId model)
        {
            var credentials = new CredentialsAccessWithModel<CredentialsModelWithTargetId>(model, AccountAccessFlags.AccessAnyCertificate);

            try
            {
                return this.Json(this.repository.GetCertificatesIssuedByAccountId(credentials));
            }
            catch (InvalidCredentialsException)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }
            catch (Exception ex)
            {
                this.logger.Error(ex);

                return BadRequest(ex);
            }
        }

        /// <summary>Deletes existing account with id specified. DeleteAccounts access level is required. Can't delete Admin.</summary>
        [HttpPost("delete_account_by_account_id")]
        public IActionResult DeleteAccountByAccountId([FromBody]CredentialsModelWithTargetId model)
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
            catch (Exception ex)
            {
                this.logger.Error(ex);

                return BadRequest(ex);
            }
        }

        /// <summary>
        /// Sets access level of a specified account to a given value.
        /// You can't change your own or Admin's access level. You can't set account's access level to be higher than yours.
        /// ChangeAccountAccessLevel access level is required.
        /// </summary>
        [HttpPost("change_account_access_level")]
        public IActionResult ChangeAccountAccessLevel([FromBody]ChangeAccountAccessLevel model)
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
            catch (Exception ex)
            {
                this.logger.Error(ex);

                return BadRequest(ex);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CertificateAuthority.Database;
using CertificateAuthority.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog;

namespace CertificateAuthority.Controllers
{
    [Produces("application/json")]
    [Route("api/accounts")]
    [ApiController]
    public sealed class AccountsController : LoggedController
    {
        private readonly DataCacheLayer repository;

        public AccountsController(DataCacheLayer repository) : base(LogManager.GetCurrentClassLogger())
        {
            this.repository = repository;
        }

        /// <summary>Provides account information of the account with id specified. AccessAccountInfo access level required.</summary>
        /// <response code="201">Instance of <see cref="AccountInfo"/>.</response>
        [HttpPost("get_account_info_by_id")]
        [ProducesResponseType(typeof(AccountInfo), 200)]
        public IActionResult GetAccountInfoById([FromBody]CredentialsModelWithTargetId model)
        {
            this.LogEntry(model);

            return ExecuteRepositoryQuery(() =>
            {
                var credentials = new CredentialsAccessWithModel<CredentialsModelWithTargetId>(model, AccountAccessFlags.AccessAccountInfo);
                return this.Json(this.repository.GetAccountInfoById(credentials));
            });
        }

        /// <summary>
        /// Provides account information of all accounts.
        /// <para>
        /// <see cref="AccountAccessFlags.AccessAccountInfo"/> access required.
        /// </para>
        /// </summary>
        /// <response code="201">Collection of <see cref="AccountModel"/> instances.</response>
        [HttpPost("list/all")]
        [ProducesResponseType(typeof(List<AccountModel>), 200)]
        public IActionResult ListAll([FromBody]CredentialsModel model)
        {
            this.LogEntry(model);

            return ExecuteRepositoryQuery(() =>
            {
                var credentials = new CredentialsAccessModel(model.AccountId, model.Password, AccountAccessFlags.AccessAccountInfo);
                return this.Json(this.repository.GetAllAccounts(credentials));
            });
        }

        /// <summary>
        /// List all unapproved accounts.
        /// <para>
        /// <see cref="AccountAccessFlags.AccessAccountInfo"/> access required.
        /// </para>
        /// </summary>
        /// <response code="201">Collection of <see cref="AccountModel"/> instances.</response>
        [HttpPost("list/unapproved")]
        [ProducesResponseType(typeof(List<AccountModel>), 200)]
        public IActionResult ListUnapproved([FromBody]CredentialsModel model)
        {
            this.LogEntry(model);

            return ExecuteRepositoryQuery(() =>
            {
                var credentials = new CredentialsAccessModel(model.AccountId, model.Password, AccountAccessFlags.AccessAccountInfo);
                return this.Json(this.repository.GetAllAccounts(credentials, true));
            });
        }

        /// <summary>Creates new account.</summary>
        /// <response code="201">Account id as integer. CreateAccounts access level is required.</response>
        [HttpPost("create")]
        [ProducesResponseType(typeof(int), 200)]
        public IActionResult CreateAccount([FromBody]CreateAccountModel model)
        {
            this.LogEntry(model);

            return ExecuteRepositoryCommand(() => this.Json(this.repository.CreateAccount(model)));
        }

        /// <summary>Marks an unapproved account as approved.</summary>
        [HttpPost("approve")]
        [ProducesResponseType(typeof(AccountModel), 200)]
        public IActionResult ApproveAccount([FromBody]CredentialsModelWithTargetId model)
        {
            this.LogEntry(model);

            return ExecuteRepositoryQuery(() =>
            {
                var credentials = new CredentialsAccessWithModel<CredentialsModelWithTargetId>(model, AccountAccessFlags.ApproveAccounts);
                return this.Json(this.repository.ApproveAccount(credentials));
            });
        }

        /// <summary>Rejects an unapproved account by removing it.</summary>
        [HttpPost("reject")]
        [ProducesResponseType(typeof(AccountModel), 200)]
        public IActionResult RejectAccount([FromBody]CredentialsModelWithTargetId model)
        {
            this.LogEntry(model);

            return ExecuteRepositoryQuery(() =>
            {
                var credentials = new CredentialsAccessWithModel<CredentialsModelWithTargetId>(model, AccountAccessFlags.ApproveAccounts);
                this.repository.RejectAccount(credentials);
                return this.Ok();
            });
        }

        /// <summary>Provides collection of all certificates issued by account with specified id. AccessAnyCertificate access level is required.</summary>
        /// <response code="201">Collection of <see cref="CertificateInfoModel"/> instances.</response>
        [HttpPost("get_certificate_issued_by_account_id")]
        [ProducesResponseType(typeof(CertificateInfoModel), 200)]
        public IActionResult GetCertificateIssuedByAccountId([FromBody]CredentialsModelWithTargetId model)
        {
            this.LogEntry(model);

            return ExecuteRepositoryQuery(() =>
            {
                var credentials = new CredentialsAccessWithModel<CredentialsModelWithTargetId>(model, AccountAccessFlags.AccessAnyCertificate);

                CertificateInfoModel cert = this.repository.GetCertificateIssuedByAccountId(credentials);

                if (cert == null)
                    return this.LogErrorExit(StatusCode(StatusCodes.Status404NotFound));

                JsonResult res = this.Json(cert);
                return res;
            });
        }

        /// <summary>Deletes existing account with id specified. DeleteAccounts access level is required. Can't delete Admin.</summary>
        [HttpPost("delete_account_by_account_id")]
        public IActionResult DeleteAccountByAccountId([FromBody]CredentialsModelWithTargetId model)
        {
            this.LogEntry(model);

            return ExecuteRepositoryCommand(() =>
            {
                var credentials = new CredentialsAccessWithModel<CredentialsModelWithTargetId>(model, AccountAccessFlags.DeleteAccounts);
                this.repository.DeleteAccount(credentials);
                return this.Ok();
            });
        }

        /// <summary>
        /// Sets access level of a specified account to a given value.
        /// You can't change your own or Admin's access level. You can't set account's access level to be higher than yours.
        /// ChangeAccountAccessLevel access level is required.
        /// </summary>
        [HttpPost("change_account_access_level")]
        public IActionResult ChangeAccountAccessLevel([FromBody]ChangeAccountAccessLevel model)
        {
            this.LogEntry(model);

            return ExecuteRepositoryCommand(() =>
            {
                var credentials = new CredentialsAccessWithModel<ChangeAccountAccessLevel>(model, AccountAccessFlags.ChangeAccountAccessLevel);
                this.repository.ChangeAccountAccessLevel(credentials);
                return Ok();
            });
        }

        /// <summary>
        /// Sets access level of a specified account to a given value.
        /// You can't change your own or Admin's access level. You can't set account's access level to be higher than yours.
        /// ChangeAccountAccessLevel access level is required.
        /// </summary>
        [HttpPost("changepassword")]
        public IActionResult ChangeAccountPassword([FromBody]ChangeAccountPasswordModel model)
        {
            this.LogEntry(model);

            return ExecuteRepositoryCommand(() =>
            {
                var credentials = new CredentialsAccessWithModel<ChangeAccountPasswordModel>(model, AccountAccessFlags.BasicAccess);
                this.repository.ChangeAccountPassword(credentials);
                return Ok();
            });
        }

        /// <summary>
        /// Executes a query against the certificate authority repository.
        /// </summary>
        /// <param name="action">The action that will execute the query.</param>
        /// <param name="memberName">The caller of the action.</param>
        /// <returns>Returns an <see cref="IActionResult"/> that is mostly a Json object.</returns>
        private IActionResult ExecuteRepositoryQuery(Func<IActionResult> action, [CallerMemberName] string memberName = "")
        {
            try
            {
                return (IActionResult)this.LogExit(action(), memberName);
            }
            catch (CertificateAuthorityAccountException ex)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status400BadRequest, ex.Message), memberName);
            }
            catch (InvalidCredentialsException ex)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status403Forbidden, ex.Message), memberName);
            }
            catch (Exception ex)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status400BadRequest, ex.ToString()), memberName);
            }
        }

        /// <summary>
        /// Executes a command (update) against the certificate authority repository.
        /// </summary>
        /// <param name="action">The action that will execute the update.</param>
        /// <param name="memberName">The caller of the action.</param>
        /// <returns>Returns an <see cref="IActionResult"/> that is mostly a OK response.</returns>
        private IActionResult ExecuteRepositoryCommand(Func<ActionResult> action, [CallerMemberName] string memberName = "")
        {
            try
            {
                return action();
            }
            catch (CertificateAuthorityAccountException ex)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status400BadRequest, ex.Message), memberName);
            }
            catch (InvalidCredentialsException ex)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status403Forbidden, ex.Message), memberName);
            }
            catch (Exception ex)
            {
                return this.LogErrorExit(StatusCode(StatusCodes.Status400BadRequest, ex.ToString()));
            }
        }
    }
}

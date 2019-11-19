using CertificateAuthority.Code.Database;
using CertificateAuthority.Code.Models;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace CertificateAuthority.Code.Controllers
{
    [Route("api/accounts")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly DataCacheLayer cache;

        public AccountsController(DataCacheLayer cache)
        {
            this.cache = cache;
        }

        /// <summary>Provides account information of the account with id specified. AccessAccountInfo access level required.</summary>
        /// <response code="201">Instance of <see cref="AccountInfo"/>.</response>
        [HttpPost("get_account_info_by_id")]
        [ProducesResponseType(typeof(AccountInfo), 200)]
        public ActionResult<AccountInfo> GetAccountInfoById([FromBody]CredentialsModelWithTargetId model)
        {
            var data = new CredentialsAccessWithModel<CredentialsModelWithTargetId>(model, AccountAccessFlags.AccessAccountInfo);

            AccountInfo result = this.cache.GetAccountInfoById(data);
            return result;
        }

        /// <summary>Provides account information of the account with id specified. AccessAccountInfo access level required.</summary>
        /// <response code="201">Collection of <see cref="AccountModel"/> instances."/>.</response>
        [HttpPost("list_accounts")]
        [ProducesResponseType(typeof(List<AccountModel>), 200)]
        public ActionResult<List<AccountModel>> GetAllAccounts([FromBody]CredentialsModel model)
        {
            var data = new CredentialsAccessModel(model.AccountId, model.Password, AccountAccessFlags.AccessAccountInfo);

            List<AccountModel> result = this.cache.GetAllAccounts(data);
            return result;
        }

        /// <summary>Creates new account.</summary>
        /// <response code="201">Account id as integer. CreateAccounts access level is required.</response>
        [HttpPost("create_account")]
        [ProducesResponseType(typeof(int), 200)]
        public ActionResult<int> CreateAccount([FromBody]CreateAccount model)
        {
            var data = new CredentialsAccessWithModel<CreateAccount>(model, AccountAccessFlags.CreateAccounts);

            int result = this.cache.CreateAccount(data);
            return result;
        }

        /// <summary>Provides collection of all certificates issued by account with specified id. AccessAnyCertificate access level is required.</summary>
        /// <response code="201">Collection of <see cref="CertificateInfoModel"/> instances."/>.</response>
        [HttpPost("get_certificates_issued_by_account_id")]
        [ProducesResponseType(typeof(List<CertificateInfoModel>), 200)]
        public ActionResult<List<CertificateInfoModel>> GetCertificatesIssuedByAccountId([FromBody]CredentialsModelWithTargetId model)
        {
            var data = new CredentialsAccessWithModel<CredentialsModelWithTargetId>(model, AccountAccessFlags.AccessAnyCertificate);

            List<CertificateInfoModel> result = this.cache.GetCertificatesIssuedByAccountId(data);

            return result;
        }

        /// <summary>Deletes existing account with id specified. DeleteAccounts access level is required. Can't delete Admin.</summary>
        [HttpPost("delete_account_by_account_id")]
        public ActionResult DeleteAccountByAccountId([FromBody]CredentialsModelWithTargetId model)
        {
            var data = new CredentialsAccessWithModel<CredentialsModelWithTargetId>(model, AccountAccessFlags.DeleteAccounts);

            this.cache.DeleteAccount(data);

            return this.Ok();
        }

        /// <summary>
        /// Sets access level of a specified account to a given value.
        /// You can't change your own or Admin's access level. You can't set account's access level to be higher than yours.
        /// ChangeAccountAccessLevel access level is required.
        /// </summary>
        [HttpPost("change_account_access_level")]
        public ActionResult ChangeAccountAccessLevel([FromBody]ChangeAccountAccessLevel model)
        {
            var data = new CredentialsAccessWithModel<ChangeAccountAccessLevel>(model, AccountAccessFlags.ChangeAccountAccessLevel);

            this.cache.ChangeAccountAccessLevel(data);
            return this.Ok();
        }
    }
}
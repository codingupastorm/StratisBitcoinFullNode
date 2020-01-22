using System;
using System.Collections.Generic;
using CertificateAuthority.Database;
using CertificateAuthority.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;

namespace CertificateAuthority.Controllers
{
    [Produces("application/json")]
    [Route("api/helpers")]
    [ApiController]
    public class HelpersController : LoggedController
    {
        private readonly DataCacheLayer cache;

        public HelpersController(DataCacheLayer cache) : base(LogManager.GetCurrentClassLogger())
        {
            this.cache = cache;
        }

        /// <summary>Calculates sha256 hash of a given string.</summary>
        /// <response code="201">Instance of string.</response>
        [HttpPost("get_sha256_hash")]
        [ProducesResponseType(typeof(string), 200)]
        public IActionResult GetSha256(string data)
        {
            this.LogEntry(data);

            try
            {                
                string hash = DataHelper.ComputeSha256Hash(data);

                return this.Json(this.LogExit(hash));
            }
            catch (Exception ex)
            {
                return this.LogErrorExit(BadRequest(ex));
            }
        }

        /// <summary>Provides collection of all access flags. To combine several flags into a single one just sum their integer representations.</summary>
        /// <param name="model">Your Id and password. <see cref="AccountAccessFlags.BasicAccess"/> access level is required.</param>
        /// <response code="201">Dictionary with access flag as key and access string as value.</response>
        [HttpPost("get_all_access_level_values")]
        [ProducesResponseType(typeof(Dictionary<int, string>), 200)]
        public IActionResult GetAllAccessLevels(CredentialsModel model)
        {
            this.LogEntry(model);

            var accessModelInfo = new CredentialsAccessModel(model.AccountId, model.Password, AccountAccessFlags.BasicAccess);

            try
            {
                this.cache.VerifyCredentialsAndAccessLevel(accessModelInfo, out AccountModel _);

                Dictionary<int, string> accesses = new Dictionary<int, string>();

                foreach (AccountAccessFlags flag in DataHelper.AllAccessFlags)
                    accesses.Add((int)flag, flag.ToString());

                string output = JsonConvert.SerializeObject(accesses);

                return this.Json(this.LogExit(output));
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
    }
}

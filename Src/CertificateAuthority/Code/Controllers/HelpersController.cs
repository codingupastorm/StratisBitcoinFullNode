﻿using System.Collections.Generic;
using CertificateAuthority.Code.Database;
using CertificateAuthority.Code.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace CertificateAuthority.Code.Controllers
{
    [Route("api/helpers")]
    [ApiController]
    public class HelpersController : ControllerBase
    {
        private readonly DataCacheLayer cache;

        public HelpersController(DataCacheLayer cache)
        {
            this.cache = cache;
        }

        /// <summary>Calculates sha256 hash of a given string.</summary>
        /// <response code="201">Instance of string.</response>
        [HttpPost("get_sha256_hash")]
        [ProducesResponseType(typeof(string), 200)]
        public ActionResult<string> GetSha256(CredentialsModelWithStringModel model)
        {
            var accessModelInfo = new CredentialsAccessModel(model.AccountId, model.Password, AccountAccessFlags.BasicAccess);

            try
            {
                this.cache.VerifyCredentialsAndAccessLevel(accessModelInfo, out AccountModel _);

                string hash = DataHelper.ComputeSha256Hash(model.Value);

                return hash;
            }
            catch (InvalidCredentialsException)
            {
                return this.Forbid();
            }
        }

        /// <summary>Provides collection of all access flags. To combine several flags into a single one just sum their integer representations.</summary>
        /// <param name="model">Your Id and password. <see cref="AccountAccessFlags.BasicAccess"/> access level is required.</param>
        /// <response code="201">Dictionary with access flag as key and access string as value.</response>
        [HttpPost("get_all_access_level_values")]
        [ProducesResponseType(typeof(Dictionary<int, string>), 200)]
        public ActionResult<string> GetAllAccessLevels(CredentialsModel model)
        {
            var accessModelInfo = new CredentialsAccessModel(model.AccountId, model.Password, AccountAccessFlags.BasicAccess);

            try
            {
                this.cache.VerifyCredentialsAndAccessLevel(accessModelInfo, out AccountModel _);

                Dictionary<int, string> accesses = new Dictionary<int, string>();

                foreach (AccountAccessFlags flag in DataHelper.AllAccessFlags)
                    accesses.Add((int)flag, flag.ToString());

                string output = JsonConvert.SerializeObject(accesses);

                return output;
            }
            catch (InvalidCredentialsException)
            {
                return this.Forbid();
            }
        }
    }
}

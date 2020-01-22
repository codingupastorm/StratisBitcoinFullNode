using System;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;

namespace CertificateAuthority.Controllers
{
    public class LoggedController : Controller
    {
        private readonly string[] privateFields = new[] { "password", "mnemonic" };

        private readonly Logger logger;

        protected LoggedController(Logger logger)
        {
            this.logger = logger;
        }

        private bool ContainsPrivateField(string str)
        {
            string lowerStr = str.ToLower();

            foreach (string privateField in this.privateFields)
                if (lowerStr.Contains(privateField))
                    return true;

            return false;
        }

        /// <summary>
        /// Serializes an object to a json string and removes the values of field names containing "password".
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns></returns>
        private string SerializeObjectWithoutPasswordValues(object obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            if (!ContainsPrivateField(json))
                return json;

            Type objectType = obj.GetType();

            var cleanedObject = Activator.CreateInstance(objectType);
            JsonConvert.PopulateObject(json, cleanedObject);

            foreach (PropertyInfo propertyInfo in objectType.GetProperties())
            {
                if (!this.ContainsPrivateField(propertyInfo.Name))
                    continue;

                propertyInfo.SetValue(cleanedObject, "******");
            }

            return JsonConvert.SerializeObject(cleanedObject);
        }

        protected void LogEntry(object model = null, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (model == null)
                this.logger.Debug($"{memberName} called.");
            else
                this.logger.Debug($"{memberName} called with { this.SerializeObjectWithoutPasswordValues(model) }.");
        }

        protected object LogExit(object res, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            this.logger.Debug($"{memberName} returning with { JsonConvert.SerializeObject(res) }.");

            return res;
        }

        protected IActionResult LogErrorExit(IActionResult res, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            this.logger.Error($"{memberName} failed with { JsonConvert.SerializeObject(res) }.");

            return res;
        }
    }
}

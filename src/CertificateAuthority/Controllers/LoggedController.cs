using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NLog;

namespace CertificateAuthority.Controllers
{
    public class LoggedController : Controller
    { 
        private readonly Logger logger;

        protected LoggedController(Logger logger)
        {
            this.logger = logger;
        }
    
        protected void LogEntry(object model = null, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (model == null)
                this.logger.Debug($"{memberName} called.");
            else
                this.logger.Debug($"{memberName} called with { JsonConvert.SerializeObject(model) }.");
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

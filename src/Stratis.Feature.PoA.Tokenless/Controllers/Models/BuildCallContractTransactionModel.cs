using System.ComponentModel.DataAnnotations;
using System.Text;
using Stratis.Bitcoin.Validations;

namespace Stratis.Feature.PoA.Tokenless.Controllers.Models
{
    public class BuildCallContractTransactionModel
    {
        [Required]
        public string Address { get; set; }

        [Required]
        public string MethodName { get; set; }

        public string[] Parameters { get; set; }
    }

    public sealed class TokenlessLocalCallModel : BuildCallContractTransactionModel
    {
    }
}
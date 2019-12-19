using System.ComponentModel.DataAnnotations;

namespace Stratis.Feature.PoA.Tokenless.Controllers.Models
{
    public sealed class BuildCallContractTransactionModel
    {
        [Required]
        public string Mnemonic { get; set; }

        [Required]
        public string Address { get; set; }

        [Required]
        public string MethodName { get; set; }

        public string[] Parameters { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;

namespace Stratis.Feature.PoA.Tokenless.Controllers.Models
{
    public sealed class SendTransactionModel
    {
        [Required]
        public string TransactionHex { get; set; }
    }
}

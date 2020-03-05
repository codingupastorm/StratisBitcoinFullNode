using System.ComponentModel.DataAnnotations;

namespace Stratis.Feature.PoA.Tokenless.Controllers.Models
{
    public sealed class SendEndorsementModel
    {
        [Required]
        public string TransactionHex { get; set; }

        [Required]
        public string Organisation { get; set; }
    }
}

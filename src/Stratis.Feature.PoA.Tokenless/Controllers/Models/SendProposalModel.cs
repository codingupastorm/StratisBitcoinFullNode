using System.ComponentModel.DataAnnotations;

namespace Stratis.Feature.PoA.Tokenless.Controllers.Models
{
    public sealed class SendProposalModel
    {
        [Required]
        public string TransactionHex { get; set; }

        [Required]
        public string Organisation { get; set; }

        public string TransientDataHex { get; set; }
    }
}

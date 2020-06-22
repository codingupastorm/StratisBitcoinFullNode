using System.ComponentModel.DataAnnotations;

namespace Stratis.Feature.PoA.Tokenless.Controllers.Models
{
    public sealed class SendProposalModel
    {
        [Required]
        public string TransactionHex { get; set; }

        public string TransientDataHex { get; set; }
    }
}

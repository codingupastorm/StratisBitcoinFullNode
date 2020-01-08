using System.ComponentModel.DataAnnotations;

namespace Stratis.Feature.PoA.Tokenless.Controllers.Models
{
    public sealed class BuildOpReturnTransactionModel
    {
        [Required]
        public byte[] OpReturnData { get; set; }
    }
}
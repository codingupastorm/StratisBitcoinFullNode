using System.ComponentModel.DataAnnotations;

namespace Stratis.Feature.PoA.Tokenless.Controllers.Models
{
    public sealed class BuildOpReturnTransactionModel
    {
        [Required]
        public string OpReturnData { get; set; }
    }
}
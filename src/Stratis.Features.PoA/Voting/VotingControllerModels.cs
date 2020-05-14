using System.ComponentModel.DataAnnotations;

namespace Stratis.Features.PoA.Voting
{
    public class HexPubKeyModel
    {
        [Required(AllowEmptyStrings = false)]
        public string PubKeyHex { get; set; }
    }
}

using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Stratis.Feature.PoA.Tokenless.Controllers.Models
{
    public sealed class TokenlessTransactionModel
    {
        [JsonProperty(PropertyName = "hex")]
        public string Hex { get; set; }

        [JsonProperty(PropertyName = "transactionId")]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 TransactionId { get; set; }
    }
}

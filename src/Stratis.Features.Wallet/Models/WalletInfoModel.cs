using System.Collections.Generic;
using Newtonsoft.Json;

namespace Stratis.Features.Wallet.Models
{
    public class WalletInfoModel
    {
        [JsonProperty(PropertyName = "walletNames")]
        public IEnumerable<string> WalletNames { get; set; }
    }
}

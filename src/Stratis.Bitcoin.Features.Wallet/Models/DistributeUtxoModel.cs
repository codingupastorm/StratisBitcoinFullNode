using System.Collections.Generic;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.MemoryPool.Broadcasting;

namespace Stratis.Bitcoin.Features.Wallet.Models
{
    public sealed class DistributeUtxoModel
    {
        [JsonProperty(PropertyName = "WalletName")]
        public string WalletName { get; set; }

        [JsonProperty(PropertyName = "UseUniqueAddressPerUtxo")]
        public bool UseUniqueAddressPerUtxo { get; set; }

        [JsonProperty(PropertyName = "UtxosCount")]
        public int UtxosCount { get; set; }

        [JsonProperty(PropertyName = "UtxoPerTransaction")]
        public int UtxoPerTransaction { get; set; }

        [JsonProperty(PropertyName = "TimestampDifferenceBetweenTransactions")]
        public int TimestampDifferenceBetweenTransactions { get; set; }

        [JsonProperty(PropertyName = "MinConfirmations")]
        public int MinConfirmations { get; set; }

        [JsonProperty(PropertyName = "DryRun")]
        public bool DryRun { get; set; }

        [JsonProperty(PropertyName = "WalletSendTransaction")]
        public List<SendTransactionModel> WalletSendTransaction { get; set; }

        public DistributeUtxoModel()
        {
            this.WalletSendTransaction = new List<SendTransactionModel>();
        }
    }
}

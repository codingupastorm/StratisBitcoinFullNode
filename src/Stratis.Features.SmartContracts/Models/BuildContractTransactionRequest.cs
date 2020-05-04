using System.ComponentModel.DataAnnotations;
using Stratis.Bitcoin.Models;
using Stratis.Bitcoin.Validations;

namespace Stratis.Features.SmartContracts.Models
{
    public class ScTxFeeEstimateRequest : TxFeeEstimateRequest
    {
        [Required(ErrorMessage = "Sender is required.")]
        [IsBitcoinAddress]
        public string Sender { get; set; }
    }

    public class BuildContractTransactionRequest : BuildTransactionRequest
    {
        [Required(ErrorMessage = "Sender is required.")]
        [IsBitcoinAddress]
        public string Sender { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;
using System.Text;
using Stratis.Bitcoin.Validations;

namespace Stratis.Feature.PoA.Tokenless.Controllers.Models
{
    public class BuildCallContractTransactionModel
    {
        [Required]
        public string Address { get; set; }

        [Required]
        public string MethodName { get; set; }

        public string[] Parameters { get; set; }
    }

    public sealed class TokenlessLocalCallModel : BuildCallContractTransactionModel
    {
        /// <summary>
        /// A wallet address containing the funds to cover transaction fees, gas, and any funds specified in the
        /// Amount field.
        /// Note that because the method call is local no funds are spent. However, the concept of the sender address
        /// is still valid and may need to be checked.
        /// For example, some methods, such as a withdrawal method on an escrow smart contract, should only be executed
        /// by the deployer, and in this case, it is the Sender address that identifies the deployer.
        /// </summary>
        [IsBitcoinAddress]
        public string Sender { get; set; }
    }
}
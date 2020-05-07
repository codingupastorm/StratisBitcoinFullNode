using System.ComponentModel.DataAnnotations;
using Stratis.Core.Utilities.ValidationAttributes;
using Stratis.Bitcoin.Validations;

namespace Stratis.Bitcoin.Models
{
    public class RecipientModel
    {
        /// <summary>
        /// The destination address.
        /// </summary>
        [Required(ErrorMessage = "A destination address is required.")]
        [IsBitcoinAddress]
        public string DestinationAddress { get; set; }

        /// <summary>
        /// The amount that will be sent.
        /// </summary>
        [Required(ErrorMessage = "An amount is required.")]
        [MoneyFormat(ErrorMessage = "The amount is not in the correct format.")]
        public string Amount { get; set; }
    }
}

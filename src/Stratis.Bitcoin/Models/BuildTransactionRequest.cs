using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Stratis.Bitcoin.Utilities.ValidationAttributes;

namespace Stratis.Bitcoin.Models
{
    /// <summary>
    /// A class containing the necessary parameters for a build transaction request.
    /// </summary>
    public class BuildTransactionRequest : TxFeeEstimateRequest, IValidatableObject
    {
        /// <summary>
        /// The fee for the transaction in STRAT (or a sidechain coin).
        /// </summary>
        [MoneyFormat(isRequired: false, ErrorMessage = "The fee is not in the correct format.")]
        public string FeeAmount { get; set; }

        /// <summary>
        /// The password for the wallet containing the funds for the transaction.
        /// </summary>
        [Required(ErrorMessage = "A password is required.")]
        public string Password { get; set; }

        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!string.IsNullOrEmpty(this.FeeAmount) && !string.IsNullOrEmpty(this.FeeType))
            {
                yield return new ValidationResult(
                    $"The query parameters '{nameof(this.FeeAmount)}' and '{nameof(this.FeeType)}' cannot be set at the same time. " +
                    $"Please use '{nameof(this.FeeAmount)}' if you'd like to set the fee manually, or '{nameof(this.FeeType)}' if you want the wallet to calculate it for you.",
                    new[] { $"{nameof(this.FeeType)}" });
            }

            if (string.IsNullOrEmpty(this.FeeAmount) && string.IsNullOrEmpty(this.FeeType))
            {
                yield return new ValidationResult(
                    $"One of parameters '{nameof(this.FeeAmount)}' and '{nameof(this.FeeType)}' is required. " +
                    $"Please use '{nameof(this.FeeAmount)}' if you'd like to set the fee manually, or '{nameof(this.FeeType)}' if you want the wallet to calculate it for you.",
                    new[] { $"{nameof(this.FeeType)}" });
            }
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace Stratis.Core.Models
{
    public class OutpointRequest : RequestModel
    {
        /// <summary>
        /// The transaction ID.
        /// </summary>
        [Required(ErrorMessage = "The transaction id is missing.")]
        public string TransactionId { get; set; }

        /// <summary>
        /// The index of the output in the transaction.
        /// </summary>
        [Required(ErrorMessage = "The index of the output in the transaction is missing.")]
        public int Index { get; set; }
    }
}

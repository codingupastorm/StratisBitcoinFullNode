using System.ComponentModel.DataAnnotations;

namespace Stratis.Bitcoin.Controllers.Models
{
    /// <summary>
    /// A class containing the necessary parameters for a wallet resynchronization request
    /// which takes the hash of the block to resync after.
    /// </summary>
    public class HashModel
    {
        /// <summary>
        /// The hash of the block to resync after.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string Hash { get; set; }
    }
}

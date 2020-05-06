using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Stratis.Core.AsyncWork.ValidationAttributes;
using Stratis.Bitcoin.Validations;

namespace Stratis.Bitcoin.Models
{
    /// <summary>
    /// A class containing the necessary parameters for a transaction fee estimate request.
    /// </summary>
    /// <seealso cref="Bitcoin.Features.Wallet.Models.RequestModel" />
    public class TxFeeEstimateRequest : RequestModel
    {
        /// <summary>
        /// The name of the wallet containing the UTXOs to use in the transaction.
        /// </summary>
        [Required(ErrorMessage = "The name of the wallet is missing.")]
        public string WalletName { get; set; }

        /// <summary>
        /// The name of the account containing the UTXOs to use in the transaction.
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// A list of outpoints to use as inputs for the transaction.
        /// </summary>
        public List<OutpointRequest> Outpoints { get; set; }

        /// <summary>
        /// A list of transaction recipients. For each recipient, specify the Pubkey script and the amount the
        /// recipient will receive in STRAT (or a sidechain coin). If the transaction was realized,
        /// both the values would be used to create the UTXOs for the transaction recipients.
        /// </summary>
        [Required(ErrorMessage = "A list of recipients is required.")]
        [MinLength(1)]
        public List<RecipientModel> Recipients { get; set; }

        /// <summary>
        /// A string containing any OP_RETURN output data to store as part of the transaction.
        /// </summary>
        public string OpReturnData { get; set; }

        /// <summary>
        /// The funds in STRAT (or a sidechain coin) to include with the OP_RETURN output. Currently, specifying
        /// some funds helps OP_RETURN outputs be relayed around the network.
        /// </summary>
        [MoneyFormat(isRequired: false, ErrorMessage = "The op return amount is not in the correct format.")]
        public string OpReturnAmount { get; set; }

        /// <summary>
        /// The type of fee to use when working out the fee for the transaction. Specify "low", "medium", or "high".
        /// </summary>
        public string FeeType { get; set; }

        /// <summary>
        /// A flag that specifies whether to include the unconfirmed amounts as inputs to the transaction.
        /// If this flag is not set, at least one confirmation is required for each input.
        /// </summary
        public bool AllowUnconfirmed { get; set; }

        /// <summary>
        /// A flag that specifies whether to shuffle the transaction outputs for increased privacy. Randomizing the
        /// the order in which the outputs appear when the transaction is being built stops it being trivial to
        /// determine whether a transaction output is payment or change. This helps defeat unsophisticated
        /// chain analysis algorithms.
        /// Defaults to true.
        /// </summary>
        public bool? ShuffleOutputs { get; set; }

        /// <summary>
        /// The address to which the change from the transaction should be returned. If this is not set,
        /// the default behaviour from the <see cref="WalletTransactionHandler"/> will be used to determine the change address.
        /// </summary>
        [IsBitcoinAddress(Required = false)]
        public string ChangeAddress { get; set; }
    }
}

using NBitcoin;

namespace Stratis.Features.SmartContracts.Interfaces
{
    /// <summary>
    /// Marks a smart contract network that has the requirement that contracts must be signed.
    /// </summary>
    public interface ISignedCodePubKeyHolder
    {
        /// <summary>
        /// The public key for the private key that contracts must be signed by.
        /// </summary>
        PubKey SigningContractPubKey { get; }
    }
}

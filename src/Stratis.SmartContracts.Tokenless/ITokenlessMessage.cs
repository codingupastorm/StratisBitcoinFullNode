namespace Stratis.SmartContracts.Tokenless
{
    public interface ITokenlessMessage
    {
        /// <summary>
        /// The address of the contract currently being executed.
        /// </summary>
        Address ContractAddress { get; }

        /// <summary>
        /// The address that called this contract.
        /// </summary>
        Address Sender { get; }
    }
}

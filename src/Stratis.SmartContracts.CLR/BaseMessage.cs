using NBitcoin;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public abstract class BaseMessage
    {
        protected BaseMessage(uint160 from, ulong amount, Gas gasLimit, string version)
        {
            this.From = from;
            this.Amount = amount;
            this.GasLimit = gasLimit;
            this.Version = version;
        }

        /// <summary>
        /// The address of the message's sender.
        /// </summary>
        public uint160 From { get; }

        /// <summary>
        /// The value sent with the message.
        /// </summary>
        public ulong Amount { get; }

        /// <summary>
        /// The maximum amount of gas that can be expended while executing the message.
        /// </summary>
        public Gas GasLimit { get; }

        /// <summary>
        /// The version to save with smart contract state data. In the current iteration this will be of the form {blockNumber}.{txNumber}
        /// </summary>
        public string Version { get; }
    }
}
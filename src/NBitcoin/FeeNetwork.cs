namespace NBitcoin
{
    public class FeeNetwork : Network
    {
        /// <summary>
        /// Mininum fee rate for all transactions.
        /// Fees smaller than this are considered zero fee for transaction creation.
        /// Be careful setting this: if you set it to zero then a transaction spammer can cheaply fill blocks using
        /// 1-satoshi-fee transactions. It should be set above the real cost to you of processing a transaction.
        /// </summary>
        /// <remarks>
        /// The <see cref="MinRelayTxFee"/> and <see cref="MinTxFee"/> are typically the same value to prevent dos attacks on the network.
        /// If <see cref="MinRelayTxFee"/> is less than <see cref="MinTxFee"/>, an attacker can broadcast a lot of transactions with fees between these two values,
        /// which will lead to transactions filling the mempool without ever being mined.
        /// </remarks>
        public long MinTxFee { get; protected set; }

        /// <summary>
        /// A fee rate that will be used when fee estimation has insufficient data.
        /// </summary>
        public long FallbackFee { get; protected set; }

        /// <summary>
        /// The minimum fee under which transactions may be rejected from being relayed.
        /// </summary>
        /// <remarks>
        /// The <see cref="MinRelayTxFee"/> and <see cref="MinTxFee"/> are typically the same value to prevent dos attacks on the network.
        /// If <see cref="MinRelayTxFee"/> is less than <see cref="MinTxFee"/>, an attacker can broadcast a lot of transactions with fees between these two values,
        /// which will lead to transactions filling the mempool without ever being mined.
        /// </remarks>
        public long MinRelayTxFee { get; protected set; }
    }
}
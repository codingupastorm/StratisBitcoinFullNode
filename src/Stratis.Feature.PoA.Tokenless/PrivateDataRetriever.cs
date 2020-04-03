using System;
using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface IPrivateDataRetriever
    {
        /// <summary>
        /// Lets the node know about some private data that has arrived in a block, so it can decide whether to ask around for it.
        /// </summary>
        /// <param name="txHash">The hash of the ReadWriteSet transaction that came in a block.</param>
        void RegisterNewPrivateData(uint256 txHash);
    }

    public class PrivateDataRetriever : IPrivateDataRetriever
    {
        /// <inheritdoc />
        public void RegisterNewPrivateData(uint256 txHash)
        {
            // Check if we have the data

            // If we do, exit do nothing.

            // If not, start the processs of asking other nodes for it.

            throw new NotImplementedException();
        }
    }
}

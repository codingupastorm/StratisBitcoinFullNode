using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Core.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Core.Utilities;

namespace Stratis.Features.Consensus
{
    /// <summary>
    /// A class that provides the ability to query consensus elements.
    /// </summary>
    public class ConsensusQuery : IGetUnspentTransaction
    {
        private readonly ICoinView coinView;

        public ConsensusQuery(ICoinView coinView)
        {
            this.coinView = coinView;
        }

        /// <inheritdoc />
        public Task<UnspentOutputs> GetUnspentTransactionAsync(uint256 trxid)
        {
            FetchCoinsResponse response = this.coinView.FetchCoins(new[] { trxid });

            UnspentOutputs unspentOutputs = response.UnspentOutputs.FirstOrDefault();

            return Task.FromResult(unspentOutputs);
        }
    }
}
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Core.Interfaces
{
    public interface IPooledTransaction
    {
        Task<Transaction> GetTransaction(uint256 trxid);
    }
}

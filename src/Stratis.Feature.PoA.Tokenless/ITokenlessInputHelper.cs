using NBitcoin;

namespace Stratis.Feature.PoA.Tokenless
{
    public interface ITokenlessInputHelper
    {
        /// <summary>
        /// Inserts a TxIn to the given transaction that allows us to identify the sender
        /// and check a signature.
        /// </summary>
        void InsertSignedTxIn(Transaction transaction, ISecret key);
    }
}

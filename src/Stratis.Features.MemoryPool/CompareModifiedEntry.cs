using System.Collections.Generic;

namespace Stratis.Features.MemoryPool
{
    /// <summary>
    /// This matches the calculation in CompareTxMemPoolEntryByAncestorFee,
    /// except operating on CTxMemPoolModifiedEntry.
    /// </summary>
    public sealed class CompareModifiedEntry : IComparer<TxMemPoolModifiedEntry>
    {
        public int Compare(TxMemPoolModifiedEntry a, TxMemPoolModifiedEntry b)
        {
            return TxMempoolEntry.CompareFees(a, b);
        }
    }
}
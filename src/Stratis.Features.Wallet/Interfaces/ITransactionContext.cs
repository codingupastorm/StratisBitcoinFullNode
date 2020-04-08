using System;

namespace Stratis.Features.Wallet.Interfaces
{
    public interface ITransactionContext : IDisposable
    {
        void Rollback();
        void Commit();
    }
}

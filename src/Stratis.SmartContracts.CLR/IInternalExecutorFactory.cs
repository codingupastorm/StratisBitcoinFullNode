using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.CLR
{
    public interface IInternalExecutorFactory
    {
        IInternalTransactionExecutor Create(RuntimeObserver.IGasMeter gasMeter, ReadWriteSet readWriteSet, IState state, string version);
    }
}
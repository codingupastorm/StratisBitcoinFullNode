using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public interface IInternalExecutorFactory
    {
        IInternalTransactionExecutor Create(IGasMeter gasMeter, ReadWriteSet readWriteSet, IState state);
    }
}
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public interface IInternalExecutorFactory
    {
        IInternalTransactionExecutor Create(IGasMeter gasMeter, ReadWriteSetBuilder readWriteSet, IState state);
    }
}
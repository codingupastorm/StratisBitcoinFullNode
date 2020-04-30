using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Factory for creating internal transaction executors
    /// </summary>
    public sealed class InternalExecutorFactory : IInternalExecutorFactory
    {
        private readonly IStateProcessor stateProcessor;

        public InternalExecutorFactory(IStateProcessor stateProcessor)
        {
            this.stateProcessor = stateProcessor;
        }

        public IInternalTransactionExecutor Create(IGasMeter gasMeter, ReadWriteSetBuilder readWriteSet, ReadWriteSetBuilder privateReadWriteSet, IState state)
        {
            return new InternalExecutor(gasMeter, readWriteSet, privateReadWriteSet,  state, this.stateProcessor);
        }
    }
}
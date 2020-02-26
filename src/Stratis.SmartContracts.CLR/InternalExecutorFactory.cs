using Stratis.SmartContracts.Core;
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
        public IInternalTransactionExecutor Create(IGasMeter gasMeter, ReadWriteSet readWriteSet, IState state)
        {
            return new InternalExecutor(gasMeter, readWriteSet, state, this.stateProcessor);
        }
    }
}
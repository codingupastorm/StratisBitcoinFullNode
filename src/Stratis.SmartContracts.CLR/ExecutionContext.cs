using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public class ExecutionContext
    {
        public ExecutionContext(Observer observer)
        {
            this.Observer = observer;
            this.ReadWriteSet = new ReadWriteSet();
        }

        public Observer Observer { get; }

        public IGasMeter GasMeter => this.Observer.GasMeter;

        public ReadWriteSet ReadWriteSet { get; }
    }
}
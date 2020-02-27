﻿using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ReadWrite;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public class ExecutionContext
    {
        public ExecutionContext(Observer observer)
        {
            this.Observer = observer;
            this.ReadWriteSet = new ReadWriteSetBuilder();
        }

        public Observer Observer { get; }

        public IGasMeter GasMeter => this.Observer.GasMeter;

        public ReadWriteSetBuilder ReadWriteSet { get; }
    }
}
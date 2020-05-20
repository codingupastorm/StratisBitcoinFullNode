using Microsoft.Extensions.Logging;
using Stratis.Core.EventBus;

namespace Stratis.Core.Signals
{
    public interface ISignals : IEventBus
    {
    }

    public class Signals : InMemoryEventBus, ISignals
    {
        public Signals(ILoggerFactory loggerFactory, ISubscriptionErrorHandler subscriptionErrorHandler) : base(loggerFactory, subscriptionErrorHandler) { }
    }
}

using System;

namespace Stratis.Core.EventBus
{
    /// <summary>
    /// Basic abstract implementation of <see cref="IEvent"/>.
    /// </summary>
    /// <seealso cref="Stratis.Core.EventBus.IEvent" />
    public abstract class EventBase
    {
        /// <inheritdoc />
        public Guid CorrelationId { get; }

        public EventBase()
        {
            // Assigns an unique id to the event.
            this.CorrelationId = Guid.NewGuid();
        }

        public override string ToString()
        {
            return $"{this.CorrelationId.ToString()} - {this.GetType().Name}";
        }
    }
}

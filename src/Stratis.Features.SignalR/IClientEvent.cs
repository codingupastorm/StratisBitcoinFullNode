using System;
using Stratis.Core.EventBus;

namespace Stratis.Features.SignalR
{
    public interface IClientEvent
    {
        Type NodeEventType { get; }

        void BuildFrom(EventBase @event);
    }
}
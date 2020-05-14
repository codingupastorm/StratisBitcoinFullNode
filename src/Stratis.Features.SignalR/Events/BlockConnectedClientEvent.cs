using System;
using Stratis.Core.EventBus;
using Stratis.Core.EventBus.CoreEvents;

namespace Stratis.Features.SignalR.Events
{
    public class BlockConnectedClientEvent : IClientEvent
    {
        public string Hash { get; set; }

        public int Height { get; set; }

        public Type NodeEventType { get; } = typeof(BlockConnected);

        public void BuildFrom(EventBase @event)
        {
            if (@event is BlockConnected blockConnected)
            {
                this.Hash = blockConnected.ConnectedBlock.ChainedHeader.HashBlock.ToString();
                this.Height = blockConnected.ConnectedBlock.ChainedHeader.Height;
                return;
            }

            throw new ArgumentException();
        }
    }
}
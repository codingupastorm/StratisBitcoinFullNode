using System;
using System.Collections.Generic;
using Stratis.Core.EventBus;
using Stratis.Features.Wallet.Models;

namespace Stratis.Features.SignalR.Events
{
    /// <summary>
    /// Marker type for Client
    /// </summary>
    public class WalletGeneralInfo
    {
    }

    public class WalletGeneralInfoClientEvent : WalletGeneralInfoModel, IClientEvent
    {
        public Type NodeEventType => typeof(WalletGeneralInfo);
        
        public IEnumerable<AccountBalanceModel> AccountsBalances { get; set; }

        public void BuildFrom(EventBase @event)
        {
            throw new NotImplementedException();
        }
    }
}
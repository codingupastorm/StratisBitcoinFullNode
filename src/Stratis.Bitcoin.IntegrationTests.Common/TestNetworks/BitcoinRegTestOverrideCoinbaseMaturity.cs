﻿using System;
using Stratis.Core.Networks;

namespace Stratis.Bitcoin.IntegrationTests.Common.TestNetworks
{
    public class BitcoinRegTestOverrideCoinbaseMaturity : BitcoinRegTest
    {
        public BitcoinRegTestOverrideCoinbaseMaturity(int coinbaseMaturity) : base()
        {
            this.Name = Guid.NewGuid().ToString("N").Substring(0, 7);
            this.Consensus.ConsensusMiningReward.CoinbaseMaturity = coinbaseMaturity;
        }
    }
}

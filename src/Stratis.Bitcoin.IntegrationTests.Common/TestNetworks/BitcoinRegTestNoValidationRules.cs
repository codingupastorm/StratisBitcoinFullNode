﻿using System;
using Stratis.Core.Networks;

namespace Stratis.Bitcoin.IntegrationTests.Common.TestNetworks
{
    public sealed class BitcoinRegTestNoValidationRules : BitcoinRegTest
    {
        public BitcoinRegTestNoValidationRules()
        {
            this.Name = Guid.NewGuid().ToString("N").Substring(0, 7);
        }
    }
}

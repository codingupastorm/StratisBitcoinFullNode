﻿using System.Collections.Generic;

namespace Stratis.Feature.PoA.Tokenless.Models
{
    public sealed class SystemChannelAddressesModel
    {
        public SystemChannelAddressesModel()
        {
            this.Addresses = new List<string>();
        }

        public List<string> Addresses { get; set; }
    }
}

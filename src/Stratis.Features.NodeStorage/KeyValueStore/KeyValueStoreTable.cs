using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Features.NodeStorage.KeyValueStore
{
    public class KeyValueStoreTable
    {
        public string TableName { get; internal set; }
        public KeyValueStoreRepository Repository { get; internal set; }
    }
}

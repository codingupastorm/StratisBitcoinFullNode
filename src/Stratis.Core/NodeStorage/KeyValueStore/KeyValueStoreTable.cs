﻿using Stratis.Core.Interfaces;

namespace Stratis.Core.NodeStorage.KeyValueStore
{
    public class KeyValueStoreTable
    {
        public string TableName { get; internal set; }

        public IKeyValueStoreRepository Repository { get; internal set; }
    }
}
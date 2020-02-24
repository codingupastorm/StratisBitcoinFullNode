﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;

namespace Stratis.Bitcoin.Features.PoA.Voting
{
    public interface IPollsKeyValueStore : IKeyValueStore
    {
    }

    public class PollsKeyValueStore : KeyValueStore<KeyValueStoreLevelDB.KeyValueStoreLevelDB>, IPollsKeyValueStore
    {
        public PollsKeyValueStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.PollsPath, loggerFactory, dateTimeProvider, repositorySerializer)
        {
        }
    }
}
﻿using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.PoA.Voting
{
    public interface IPollsKeyValueStore : IKeyValueStore
    {
    }

    public class PollsKeyValueStore : Bitcoin.KeyValueStoreLevelDB.KeyValueStoreLevelDB, IPollsKeyValueStore
    {
        public PollsKeyValueStore(IRepositorySerializer repositorySerializer, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider)
            : base(dataFolder.PollsPath, loggerFactory, repositorySerializer)
        {
        }
    }
}
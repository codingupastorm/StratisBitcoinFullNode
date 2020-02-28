using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.KeyValueStoreLevelDB;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SmartContracts.Core.Store
{
    public interface ITransientKeyValueStore : IKeyValueRepositoryStore
    {

    }

    /// <summary>
    /// The underlying levelDB database for the transient store
    /// </summary>
    public class TransientKeyValueStore : KeyValueStoreLevelDB, ITransientKeyValueStore
    {
        public TransientKeyValueStore(DataFolder dataFolder, ILoggerFactory loggerFactory, IRepositorySerializer repositorySerializer) 
            : base(loggerFactory, repositorySerializer)
        {

        }
    }

    /// <summary>
    /// Implements the transient store operations on top of the ITransientKeyValueStore database
    /// </summary>
    public class TransientStore
    {
        private readonly ITransientKeyValueStore repository;

        public TransientStore(ITransientKeyValueStore repository)
        {
            this.repository = repository;
        }


    }
}

using System;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.NodeStorage.Interfaces
{
    /// <summary>
    /// Used to register and obtain stores by name.
    /// </summary>
    public interface INodeStorageProvider
    {
        void RegisterStoreProvider(string name, Func<NodeStorageProvider, IRepositorySerializer, IKeyValueStore> creator, IRepositorySerializer repositorySerializer);
        IKeyValueStore GetStore(string name);
    }
}

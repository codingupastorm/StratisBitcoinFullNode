using NBitcoin;
using Stratis.Patricia;

namespace Stratis.SmartContracts.Core.State
{
    public class StateRepoV2
    {
        private readonly ISource<byte[], byte[]> persistentDb;
        private readonly IPatriciaTrie patriciaTrie;
        private readonly ISource<byte[], AccountState> accountStateCache;

        public StateRepoV2(ISource<byte[],byte[]> db, byte[] root)
        {
            this.persistentDb = db;
            this.patriciaTrie = new PatriciaTrie(root, this.persistentDb);
            SourceCodec<byte[], AccountState, byte[], byte[]> accountStateCodec = new SourceCodec<byte[], AccountState, byte[], byte[]>(this.patriciaTrie, new Serializers.NoSerializer<byte[]>(), Serializers.AccountSerializer);
            // Should be readcache.
            this.accountStateCache = new ReadWriteCache<AccountState>(accountStateCodec, WriteCache<AccountState>.CacheType.SIMPLE);
        }

        public AccountState GetAccountState(uint160 address)
        {
            return this.accountStateCache.Get(address.ToBytes());
        }

        public MutableState GetMutableState()
        {
            ISource<byte[], byte[]> cachedSource = new WriteCache<byte[]>(this.persistentDb, WriteCache<byte[]>.CacheType.SIMPLE);
            return new MutableState(cachedSource, this.patriciaTrie.GetRootHash(), this);
        }

        public void SyncToRoot(byte[] root)
        {
            this.patriciaTrie.SetRootHash(root);
        }

    }

    public class MutableState
    {
        private readonly ISource<byte[], byte[]> underlyingSource;
        private readonly IPatriciaTrie patriciaTrie;
        private readonly ISource<byte[], AccountState> accountStateCache;
        private readonly StateRepoV2 parent;

        public MutableState(ISource<byte[], byte[]> source, byte[] root, StateRepoV2 parent)
        {
            this.underlyingSource = source;
            this.patriciaTrie = new PatriciaTrie(root, source);
            this.accountStateCache = new SourceCodec<byte[], AccountState, byte[], byte[]>(this.patriciaTrie, new Serializers.NoSerializer<byte[]>(), Serializers.AccountSerializer);

            this.parent = parent;
        }

        public AccountState GetAccountState(uint160 address)
        {
            return this.accountStateCache.Get(address.ToBytes());
        }

        public void CommitToDb()
        {
            this.accountStateCache.Flush();
            this.patriciaTrie.Flush();
            this.underlyingSource.Flush();
            this.parent.SyncToRoot(this.patriciaTrie.GetRootHash());
        }

        public byte[] GetRoot()
        {
            this.accountStateCache.Flush();
            return this.patriciaTrie.GetRootHash();
        }

        public AccountState CreateAccount(uint160 addr)
        {
            AccountState state = new AccountState();
            this.accountStateCache.Put(addr.ToBytes(), state);
            return state;
        }

        public void SyncToRoot(byte[] root)
        {
            this.patriciaTrie.SetRootHash(root);
        }
    }
}

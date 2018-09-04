using Stratis.Patricia;
using Stratis.SmartContracts.Core.State;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class StateRepoV2Tests
    {

        [Fact]
        public void Test()
        {
            var memDb = new MemoryDictionarySource();
            ISource<byte[], byte[]> stateDB = new NoDeleteSource<byte[], byte[]>(memDb);
            StateRepoV2 repo = new StateRepoV2(stateDB, null);
            MutableState mutableState = repo.GetMutableState();
            mutableState.CreateAccount(123);
            Assert.NotNull(mutableState.GetAccountState(123));
            Assert.Empty(memDb.Db);
            Assert.Null(repo.GetAccountState(123));
            mutableState.CommitToDb();
            Assert.NotNull(repo.GetAccountState(123));

            byte[] snapshot = mutableState.GetRoot();
            mutableState.CreateAccount(12345);
            byte[] after = mutableState.GetRoot();
            Assert.NotNull(mutableState.GetAccountState(12345));
            mutableState.SyncToRoot(snapshot);
            Assert.Null(mutableState.GetAccountState(12345));
            Assert.NotNull(mutableState.GetAccountState(123));
        }
    }
}

using NBitcoin;
using Stratis.Features.ContractEndorsement.State;
using Stratis.SmartContracts.CLR.Caching;
using Stratis.SmartContracts.CLR.Compilation;
using Stratis.SmartContracts.CLR.Loader;
using Xunit;

namespace Stratis.Features.ContractEndorsement.Tests
{
    public class ExecutorTests
    {
        private readonly HyperContractExecutor executor;
        private readonly Database<uint160, ContractState> contractDb;
        private readonly ByteArrayDatabase<byte[]> codeDb;
        private readonly Database<CacheKey, StorageValue> contractStorageDb;
        private readonly FinalisedStateDb db;

        public ExecutorTests()
        {
            this.contractDb = new Database<uint160, ContractState>();
            this.codeDb = new ByteArrayDatabase<byte[]>();
            this.contractStorageDb = new Database<CacheKey, StorageValue>();
            this.db = new FinalisedStateDb(this.contractDb, this.codeDb, this.contractStorageDb);

            this.executor = new HyperContractExecutor(new ContractAssemblyLoader(), new ContractModuleDefinitionReader(), new ContractAssemblyCache(), this.db);
        }

        [Fact]
        public void TestContractCreation()
        {
            var compilationResult = ContractCompiler.CompileFile("SmartContracts/TestContract.cs");
            byte[] contractCode = compilationResult.Compilation;

            this.executor.Create(contractCode, new object[0], "TestContract", 0);
        }
    }
}

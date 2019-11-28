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

        public ExecutorTests()
        {
            FinalisedStateDb contractStateDb = null;

            this.executor = new HyperContractExecutor(new ContractAssemblyLoader(), new ContractModuleDefinitionReader(), new ContractAssemblyCache(), contractStateDb);
        }

        [Fact]
        public void TestContractCreation()
        {
        }
    }
}

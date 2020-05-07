using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Core.Utilities;
using Stratis.Features.Consensus.ProvenBlockHeaders;
using Xunit;

namespace Stratis.Features.Consensus.Tests.ProvenBlockHeaders
{
    public class ProvenBlockHeaderRepositoryTests : LogsTestBase
    {
        private readonly Mock<ILoggerFactory> loggerFactory;
        private readonly RepositorySerializer repositorySerializer;
        private const string ProvenBlockHeaderTable = "ProvenBlockHeader";
        private const string BlockHashTable = "BlockHashHeight";

        public ProvenBlockHeaderRepositoryTests() : base(KnownNetworks.StratisTest)
        {
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.repositorySerializer = new RepositorySerializer(this.Network.Consensus.ConsensusFactory);
        }

        [Fact]
        public void Initializes_Genesis_ProvenBlockHeader_OnLoadAsync()
        {
            string folder = CreateTestDir(this);

            // Initialise the repository - this will set-up the genesis blockHash (blockId).
            (IProvenBlockHeaderRepository repo, IProvenBlockHeaderKeyValueStore store) = this.SetupRepository(this.Network, folder);

            using (repo)
            {
                // Check the BlockHash (blockId) exists.
                repo.TipHashHeight.Height.Should().Be(0);
                repo.TipHashHeight.Hash.Should().Be(this.Network.GetGenesis().GetHash());
            }

            store.Dispose();
        }

        [Fact]
        public async Task PutAsync_WritesProvenBlockHeaderAndSavesBlockHashAsync()
        {
            string folder = CreateTestDir(this);

            ProvenBlockHeader provenBlockHeaderIn = CreateNewProvenBlockHeaderMock();

            var blockHashHeightPair = new HashHeightPair(provenBlockHeaderIn.GetHash(), 0);
            var items = new SortedDictionary<int, ProvenBlockHeader>() { { 0, provenBlockHeaderIn } };

            (IProvenBlockHeaderRepository repo, IProvenBlockHeaderKeyValueStore store) = this.SetupRepository(this.Network, folder);

            using (repo)
            {
                await repo.PutAsync(items, blockHashHeightPair);

                using (IKeyValueStoreTransaction txn = store.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, ProvenBlockHeaderTable, BlockHashTable))
                {
                    txn.Select(ProvenBlockHeaderTable, blockHashHeightPair.Height, out ProvenBlockHeader headerOut);
                    txn.Select(BlockHashTable, new byte[0], out HashHeightPair hashHeightPairOut);

                    headerOut.Should().NotBeNull();
                    headerOut.GetHash().Should().Be(provenBlockHeaderIn.GetHash());

                    hashHeightPairOut.Should().NotBeNull();
                    hashHeightPairOut.Hash.Should().Be(provenBlockHeaderIn.GetHash());
                }
            }
        }

        [Fact]
        public async Task PutAsync_Inserts_MultipleProvenBlockHeadersAsync()
        {
            string folder = CreateTestDir(this);

            PosBlock posBlock = CreatePosBlock();
            ProvenBlockHeader header1 = CreateNewProvenBlockHeaderMock(posBlock);
            ProvenBlockHeader header2 = CreateNewProvenBlockHeaderMock(posBlock);

            var items = new SortedDictionary<int, ProvenBlockHeader>() { { 0, header1 }, { 1, header2 } };

            // Put the items in the repository.
            (IProvenBlockHeaderRepository repo, IProvenBlockHeaderKeyValueStore store) = this.SetupRepository(this.Network, folder);

            using (repo)
            {
                await repo.PutAsync(items, new HashHeightPair(header2.GetHash(), items.Count - 1));

                // Check the ProvenBlockHeader exists in the database.
                using (IKeyValueStoreTransaction txn = store.CreateTransaction(KeyValueStoreTransactionMode.Read, ProvenBlockHeaderTable))
                {
                    var headersOut = txn.SelectDictionary<int, ProvenBlockHeader>(ProvenBlockHeaderTable);

                    headersOut.Keys.Count.Should().Be(2);
                    headersOut.First().Value.GetHash().Should().Be(items[0].GetHash());
                    headersOut.Last().Value.GetHash().Should().Be(items[1].GetHash());
                }
            }
        }

        [Fact]
        public async Task GetAsync_ReadsProvenBlockHeaderAsync()
        {
            string folder = CreateTestDir(this);

            ProvenBlockHeader headerIn = CreateNewProvenBlockHeaderMock();

            int blockHeight = 1;

            (IProvenBlockHeaderRepository repo, IProvenBlockHeaderKeyValueStore store) = this.SetupRepository(this.Network, folder);

            using (IKeyValueStoreTransaction txn = store.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, ProvenBlockHeaderTable))
            {
                txn.Insert(ProvenBlockHeaderTable, blockHeight, headerIn);
                txn.Commit();
            }

            repo.Dispose();
            store.Dispose();

            (repo, store) = this.SetupRepository(this.Network, folder);

            // Query the repository for the item that was inserted in the above code.
            using (repo)
            {
                var headerOut = await repo.GetAsync(blockHeight).ConfigureAwait(false);

                headerOut.Should().NotBeNull();
                uint256.Parse(headerOut.ToString()).Should().Be(headerOut.GetHash());
            }

            store.Dispose();
        }

        [Fact]
        public async Task GetAsync_WithWrongBlockHeightReturnsNullAsync()
        {
            string folder = CreateTestDir(this);

            (IProvenBlockHeaderRepository repo, IProvenBlockHeaderKeyValueStore store) = this.SetupRepository(this.Network, folder);

            using (IKeyValueStoreTransaction txn = store.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, ProvenBlockHeaderTable, BlockHashTable))
            {
                txn.Insert(ProvenBlockHeaderTable, 1, CreateNewProvenBlockHeaderMock());
                txn.Insert(BlockHashTable, new byte[0], new HashHeightPair(new uint256(), 1));
                txn.Commit();
            }

            repo.Dispose();
            store.Dispose();

            (repo, store) = this.SetupRepository(this.Network, folder);

            using (repo)
            {
                // Select a different block height.
                ProvenBlockHeader outHeader = await repo.GetAsync(2).ConfigureAwait(false);
                outHeader.Should().BeNull();

                // Select the original item inserted into the table
                outHeader = await repo.GetAsync(1).ConfigureAwait(false);
                outHeader.Should().NotBeNull();
            }
        }

        /// <summary>
        /// PutAsync_Add_Ten_ProvenBlockHeaders_Dispose_On_Initialise_Repo_TipHeight_Should_Be_At_Last_Saved_TipAsync
        /// </summary>
        [Fact]
        public async Task ProvenBlockHeaderRepositoryTest_Scenario6_Async()
        {
            string folder = CreateTestDir(this);

            PosBlock posBlock = CreatePosBlock();
            var headers = new SortedDictionary<int, ProvenBlockHeader>();

            for (int i = 0; i < 10; i++)
            {
                headers.Add(i, CreateNewProvenBlockHeaderMock(posBlock));
            }

            // Put the items in the repository.
            (IProvenBlockHeaderRepository repo, IProvenBlockHeaderKeyValueStore store) = this.SetupRepository(this.Network, folder);

            using (repo)
            {
                await repo.PutAsync(headers, new HashHeightPair(headers.Last().Value.GetHash(), headers.Count - 1));
            }

            repo.Dispose();
            store.Dispose();

            (IProvenBlockHeaderRepository newRepo, IProvenBlockHeaderKeyValueStore newStore) = this.SetupRepository(this.Network, folder);

            using (newRepo)
            {
                newRepo.TipHashHeight.Hash.Should().Be(headers.Last().Value.GetHash());
                newRepo.TipHashHeight.Height.Should().Be(headers.Count - 1);
            }
        }

        private (ProvenBlockHeaderRepository, ProvenBlockHeaderKeyValueStore) SetupRepository(Network network, string folder)
        {
            var loggerFactory = new LoggerFactory();
            var dateTimeProvider = new DateTimeProvider();

            var store = new ProvenBlockHeaderKeyValueStore(network, new DataFolder(folder), loggerFactory, dateTimeProvider, this.repositorySerializer);
            var repo = new ProvenBlockHeaderRepository(store, network, folder, loggerFactory, this.repositorySerializer);

            Task task = repo.InitializeAsync();

            task.Wait();

            return (repo, store);
        }
    }
}

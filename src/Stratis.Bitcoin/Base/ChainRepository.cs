using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.Core.Utilities;

namespace Stratis.Bitcoin.Base
{
    public interface IChainRepository : IDisposable
    {
        /// <summary>Loads the chain of headers from the database.</summary>
        /// <returns>Tip of the loaded chain.</returns>
        Task<ChainedHeader> LoadAsync(ChainedHeader genesisHeader);

        /// <summary>Persists chain of headers to the database.</summary>
        Task SaveAsync(ChainIndexer chainIndexer);
    }

    public class ChainRepository : IChainRepository
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Access to DBreeze database.</summary>
        private readonly IChainRepositoryStore keyValueStore;

        private BlockLocator locator;

        public ChainRepository(IChainRepositoryStore chainRepositoryStore, ILoggerFactory loggerFactory)
        {
            Guard.NotNull(chainRepositoryStore, nameof(chainRepositoryStore));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.keyValueStore = chainRepositoryStore;
        }

        /// <inheritdoc />
        public Task<ChainedHeader> LoadAsync(ChainedHeader genesisHeader)
        {
            Task<ChainedHeader> task = Task.Run(() =>
            {
                using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read))
                {
                    ChainedHeader tip = null;
                    if (!transaction.Select<int, BlockHeader>("Chain", 0, out BlockHeader previousHeader))
                        return genesisHeader;

                    Guard.Assert(previousHeader.GetHash() == genesisHeader.HashBlock); // can't swap networks

                    foreach ((int key, BlockHeader blockHeader) in transaction.SelectAll<int, BlockHeader>("Chain").Skip(1))
                    {
                        if ((tip != null) && (previousHeader.HashPrevBlock != tip.HashBlock))
                            break;

                        tip = new ChainedHeader(previousHeader, blockHeader.HashPrevBlock, tip);
                        previousHeader = blockHeader;
                    }

                    if (previousHeader != null)
                        tip = new ChainedHeader(previousHeader, previousHeader.GetHash(), tip);

                    if (tip == null)
                        tip = genesisHeader;

                    this.locator = tip.GetLocator();
                    return tip;
                }
            });

            return task;
        }

        /// <inheritdoc />
        public Task SaveAsync(ChainIndexer chainIndexer)
        {
            Guard.NotNull(chainIndexer, nameof(chainIndexer));

            Task task = Task.Run(() =>
            {
                using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, "Chain"))
                {
                    ChainedHeader fork = this.locator == null ? null : chainIndexer.FindFork(this.locator);
                    ChainedHeader tip = chainIndexer.Tip;
                    ChainedHeader toSave = tip;

                    var headers = new List<ChainedHeader>();
                    while (toSave != fork)
                    {
                        headers.Add(toSave);
                        toSave = toSave.Previous;
                    }

                    // DBreeze is faster on ordered insert.
                    IOrderedEnumerable<ChainedHeader> orderedChainedHeaders = headers.OrderBy(b => b.Height);
                    foreach (ChainedHeader block in orderedChainedHeaders)
                    {
                        BlockHeader header = block.Header;
                        if (header is ProvenBlockHeader)
                        {
                            // copy the header parameters, untill we dont make PH a normal header we store it in its own repo.
                            BlockHeader newHeader = chainIndexer.Network.Consensus.ConsensusFactory.CreateBlockHeader();
                            newHeader.Bits = header.Bits;
                            newHeader.Time = header.Time;
                            newHeader.Nonce = header.Nonce;
                            newHeader.Version = header.Version;
                            newHeader.HashMerkleRoot = header.HashMerkleRoot;
                            newHeader.HashPrevBlock = header.HashPrevBlock;

                            header = newHeader;
                        }

                        transaction.Insert("Chain", block.Height, header);
                    }

                    this.locator = tip.GetLocator();
                    transaction.Commit();
                }
            });

            return task;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}

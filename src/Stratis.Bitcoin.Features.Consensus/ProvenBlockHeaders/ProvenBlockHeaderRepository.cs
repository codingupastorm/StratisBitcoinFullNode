using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.KeyValueStore;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus.ProvenBlockHeaders
{
    public interface IProvenBlockHeaderKeyValueStore : IKeyValueStore
    {
    }

    public class ProvenBlockHeaderKeyValueStore : KeyValueStore<KeyValueStoreLevelDB.KeyValueStoreLevelDB>, IProvenBlockHeaderKeyValueStore
    {
        public ProvenBlockHeaderKeyValueStore(Network network, DataFolder dataFolder, ILoggerFactory loggerFactory, IDateTimeProvider dateTimeProvider, IRepositorySerializer repositorySerializer)
            : base(new KeyValueStoreLevelDB.KeyValueStoreLevelDB(loggerFactory, repositorySerializer))
        {
            this.Repository.Init(dataFolder.ProvenBlockHeaderPath);
        }
    }

    /// <summary>
    /// Persistent implementation of the <see cref="ProvenBlockHeader"/> DBreeze repository.
    /// </summary>
    public class ProvenBlockHeaderRepository : IProvenBlockHeaderRepository
    {
        /// <summary>
        /// Instance logger.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Access to a key-value store database.
        /// </summary>
        public readonly IKeyValueStore keyValueStore;

        /// <summary>
        /// Specification of the network the node runs on - RegTest/TestNet/MainNet.
        /// </summary>
        private readonly Network network;

        /// <summary>
        /// Database key under which the block hash and height of a <see cref="ProvenBlockHeader"/> tip is stored.
        /// </summary>
        private static readonly byte[] blockHashHeightKey = new byte[0];

        /// <summary>
        /// DBreeze table names.
        /// </summary>
        private const string ProvenBlockHeaderTable = "ProvenBlockHeader";
        private const string BlockHashHeightTable = "BlockHashHeight";

        /// <summary>
        /// Current <see cref="ProvenBlockHeader"/> tip.
        /// </summary>
        private ProvenBlockHeader provenBlockHeaderTip;

        private readonly RepositorySerializer repositorySerializer;

        /// <inheritdoc />
        public HashHeightPair TipHashHeight { get; private set; }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="provenBlockHeaderKeyValueStore">The key-value database to use.</param>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="folder"><see cref="ProvenBlockHeaderRepository"/> folder path to the database files.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="repositorySerializer">The serializer to use for <see cref="IBitcoinSerializable"/> objects.</param>
        public ProvenBlockHeaderRepository(IProvenBlockHeaderKeyValueStore provenBlockHeaderKeyValueStore, Network network, DataFolder folder, ILoggerFactory loggerFactory,
            RepositorySerializer repositorySerializer)
        : this(provenBlockHeaderKeyValueStore, network, folder.ProvenBlockHeaderPath, loggerFactory, repositorySerializer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the object.
        /// </summary>
        /// <param name="provenBlockHeaderKeyValueStore">The key-value database to use.</param>
        /// <param name="network">Specification of the network the node runs on - RegTest/TestNet/MainNet.</param>
        /// <param name="folder"><see cref="ProvenBlockHeaderRepository"/> folder path to the database files.</param>
        /// <param name="loggerFactory">Factory to create a logger for this type.</param>
        /// <param name="repositorySerializer">The serializer to use for <see cref="IBitcoinSerializable"/> objects.</param>
        public ProvenBlockHeaderRepository(IProvenBlockHeaderKeyValueStore provenBlockHeaderKeyValueStore, Network network, string folder, ILoggerFactory loggerFactory,
            RepositorySerializer repositorySerializer)
        {
            Guard.NotNull(provenBlockHeaderKeyValueStore, nameof(provenBlockHeaderKeyValueStore));
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(folder, nameof(folder));
            this.repositorySerializer = repositorySerializer;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            Directory.CreateDirectory(folder);

            this.keyValueStore = provenBlockHeaderKeyValueStore;
            this.network = network;
        }

        /// <inheritdoc />
        public Task InitializeAsync()
        {
            Task task = Task.Run(() =>
            {
                using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, BlockHashHeightTable))
                {
                    this.TipHashHeight = this.GetTipHash(transaction);

                    if (this.TipHashHeight != null)
                        return;

                    var hashHeight = new HashHeightPair(this.network.GetGenesis().GetHash(), 0);

                    this.SetTip(transaction, hashHeight);

                    transaction.Commit();

                    this.TipHashHeight = hashHeight;
                }
            });

            return task;
        }

        /// <inheritdoc />
        public Task<ProvenBlockHeader> GetAsync(int blockHeight)
        {
            Task<ProvenBlockHeader> task = Task.Run(() =>
            {
                using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.Read, ProvenBlockHeaderTable))
                {
                    if (!transaction.Select(ProvenBlockHeaderTable, blockHeight, out ProvenBlockHeader result))
                        return null;

                    return result;
                }
            });

            return task;
        }

        /// <inheritdoc />
        public Task PutAsync(SortedDictionary<int, ProvenBlockHeader> headers, HashHeightPair newTip)
        {
            Guard.NotNull(headers, nameof(headers));
            Guard.NotNull(newTip, nameof(newTip));

            Guard.Assert(newTip.Hash == headers.Values.Last().GetHash());

            Task task = Task.Run(() =>
            {
                this.logger.LogDebug("({0}.Count():{1})", nameof(headers), headers.Count());

                using (IKeyValueStoreTransaction transaction = this.keyValueStore.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, ProvenBlockHeaderTable, BlockHashHeightTable))
                {
                    this.InsertHeaders(transaction, headers);

                    this.SetTip(transaction, newTip);

                    transaction.Commit();

                    this.TipHashHeight = newTip;
                }
            });

            return task;
        }

        /// <summary>
        /// Set's the hash and height tip of the new <see cref="ProvenBlockHeader"/>.
        /// </summary>
        /// <param name="transaction"> Open DBreeze transaction.</param>
        /// <param name="newTip"> Hash height pair of the new block tip.</param>
        private void SetTip(IKeyValueStoreTransaction transaction, HashHeightPair newTip)
        {
            Guard.NotNull(newTip, nameof(newTip));

            transaction.Insert(BlockHashHeightTable, blockHashHeightKey, newTip);
        }

        /// <summary>
        /// Inserts <see cref="ProvenBlockHeader"/> items into to the database.
        /// </summary>
        /// <param name="transaction"> Open DBreeze transaction.</param>
        /// <param name="headers"> List of <see cref="ProvenBlockHeader"/> items to save.</param>
        private void InsertHeaders(IKeyValueStoreTransaction transaction, SortedDictionary<int, ProvenBlockHeader> headers)
        {
            foreach (KeyValuePair<int, ProvenBlockHeader> header in headers)
                transaction.Insert(ProvenBlockHeaderTable, header.Key, header.Value);

            // Store the latest ProvenBlockHeader in memory.
            this.provenBlockHeaderTip = headers.Last().Value;
        }

        /// <summary>
        /// Retrieves the current <see cref="HashHeightPair"/> tip from disk.
        /// </summary>
        /// <param name="transaction"> Open DBreeze transaction.</param>
        /// <returns> Hash of blocks current tip.</returns>
        private HashHeightPair GetTipHash(IKeyValueStoreTransaction transaction)
        {
            if (!transaction.Select(BlockHashHeightTable, blockHashHeightKey, out HashHeightPair result))
                return null;

            return result;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}

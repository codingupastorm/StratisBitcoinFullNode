using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Core.Configuration;
using Stratis.Core.Interfaces;
using Stratis.Core.NodeStorage.KeyValueStoreLevelDB;
using Stratis.Core.Utilities;

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
            : base(dataFolder.TransientStorePath, loggerFactory, repositorySerializer)
        {
        }
    }

    public interface ITransientStore
    {
        void Persist(uint256 id, uint blockHeight, TransientStorePrivateData data);

        (TransientStorePrivateData Data, uint BlockHeight) Get(uint256 txId);

        void Purge(uint256 txId);
    }

    /// <summary>
    /// Implements the transient store operations on top of the ITransientKeyValueStore database
    /// </summary>
    public class TransientStore : ITransientStore
    {
        public const string Table = "transient";
        public byte[] MinBlockHeightKey = Encoding.ASCII.GetBytes("MinBlockHeight");

        private readonly ITransientKeyValueStore repository;

        public TransientStore(ITransientKeyValueStore repository)
        {
            this.repository = repository;
        }

        public void Persist(uint256 id, uint blockHeight, TransientStorePrivateData data)
        {
            var uuid = Guid.NewGuid();
            var key = TransientStoreQueryParams.CreateCompositeKeyForPvtRWSet(blockHeight, id, uuid);
            var compositePurgeIndexKey = TransientStoreQueryParams.CreateCompositeKeyForPurgeIndexByHeight(blockHeight, id, uuid);

            using (IKeyValueStoreTransaction tx = this.repository.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, Table))
            {
                var hasValue = tx.Select(Table, MinBlockHeightKey, out uint minBlockHeight);

                // Update the min block height if necessary.
                if (!hasValue || minBlockHeight > blockHeight)
                {
                    tx.Insert(Table, MinBlockHeightKey, blockHeight);
                }

                tx.Insert(Table, key, data.ToBytes());
                tx.Insert(Table, compositePurgeIndexKey, new byte[] { });
                tx.Commit();
            }
        }

        /// <summary>
        /// Returns the private data changes a particular transaction made. 
        /// </summary>
        public (TransientStorePrivateData Data, uint BlockHeight) Get(uint256 txId)
        {
            // This could surely be more efficient.

            using (IKeyValueStoreTransaction tx = this.repository.CreateTransaction(KeyValueStoreTransactionMode.Read, Table))
            {
                var keyStart = TransientStoreQueryParams.CreateTxIdRangeStartKey(txId);
                var keyEnd = TransientStoreQueryParams.CreateTxIdRangeEndKey(txId);

                IEnumerable<(byte[], byte[])> values = tx.SelectForward<byte[], byte[]>(Table, keyStart, keyEnd);

                foreach ((byte[] Key, byte[] Value) record in values)
                {
                    (uint256 recordTxId, Guid _, uint blockHeight) = TransientStoreQueryParams.SplitCompositeKeyForPvtRWSet(record.Key);

                    if (recordTxId == txId)
                    {
                        return (new TransientStorePrivateData(record.Value), blockHeight);
                    }
                }

                return (null, 0);
            }
        }

        /// <summary>
        /// Returns the lowest block height for the data remaining in the transient store.
        /// </summary>
        /// <returns></returns>
        public uint GetMinBlockHeight()
        {
            using (IKeyValueStoreTransaction tx = this.repository.CreateTransaction(KeyValueStoreTransactionMode.Read, Table))
            {
                return !tx.Select(Table, this.MinBlockHeightKey, out uint minBlockHeight) ? 0 : minBlockHeight;
            }
        }

        /// <summary>
        /// Purge entries from the transient store by txid.
        /// </summary>
        /// <param name="txId"></param>
        public void Purge(uint256 txId)
        {
            using (IKeyValueStoreTransaction tx = this.repository.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, Table))
            {
                var purgeKeyStart = TransientStoreQueryParams.CreateTxIdRangeStartKey(txId);
                var purgeKeyEnd = TransientStoreQueryParams.CreateTxIdRangeEndKey(txId);

                var values = tx.SelectForward<byte[], byte[]>(Table, purgeKeyStart, purgeKeyEnd, true, includeLastKey: false, keysOnly: true);

                // We can safely expect only a single value here due to the range query start key being the same as the end key.
                var record = values.FirstOrDefault();

                if (record.Item1 != null)
                {
                    var key = record.Item1;

                    // We're in the right range
                    // Explode key
                    (uint256 id, Guid uuid, uint blockHeight) = TransientStoreQueryParams.SplitCompositeKeyForPvtRWSet(key);

                    // Remove data key value
                    var dataKey = TransientStoreQueryParams.CreateCompositeKeyForPvtRWSet(blockHeight, id, uuid);
                    tx.RemoveKey(Table, dataKey, (object)null);

                    // Remove block height purge key
                    var purgeKey = TransientStoreQueryParams.CreateCompositeKeyForPurgeIndexByHeight(blockHeight, id, uuid);
                    tx.RemoveKey(Table, purgeKey, (object)null);

                    // Update current min block height
                    if (this.GetMinBlockHeight() == blockHeight)
                    {
                        UpdateMinBlockHeight(tx, blockHeight);
                    }

                    // Commit
                    tx.Commit();
                }
            }
        }

        private void UpdateMinBlockHeight(IKeyValueStoreTransaction tx, uint oldMinBlockHeight)
        {
            var startKey = TransientStoreQueryParams.CreatePurgeIndexByHeightRangeStartKey(oldMinBlockHeight + 1);

            // We know we're removing the key at height, so ignore it in our query
            var values = tx.SelectForward<byte[], byte[]>(Table, startKey, keysOnly: true);

            (byte[], byte[]) minKey = values.FirstOrDefault();

            if (minKey.Item1 != null)
            {
                (uint256 txId, Guid uuid, uint keyBlockHeight) explode = TransientStoreQueryParams.SplitCompositeKeyOfPurgeIndexByHeight(minKey.Item1);

                tx.Insert(Table, MinBlockHeightKey, explode.keyBlockHeight);
            }
            else
            {
                // No purge keys left, no min block height left, can remove the key.
                tx.RemoveKey(Table, this.MinBlockHeightKey, (object)null);
            }
        }

        /// <summary>
        /// Purge entries from the transient store that are below a block height.
        /// </summary>
        /// <param name="height"></param>
        public void PurgeBelowHeight(uint height)
        {
            using (IKeyValueStoreTransaction tx = this.repository.CreateTransaction(KeyValueStoreTransactionMode.ReadWrite, Table))
            {
                var purgeKeyStart = TransientStoreQueryParams.CreatePurgeIndexByHeightRangeStartKey(0);
                var purgeKeyEnd = TransientStoreQueryParams.CreatePurgeIndexByHeightRangeEndKey(height - 1);

                // TODO: We may be able to remove some of the complexity here given that the query is now ranged.

                var values = tx.SelectForward<byte[], byte[]>(Table, purgeKeyStart, purgeKeyEnd, true);

                foreach (var record in values)
                {
                    var key = record.Item1;

                    // Query for keys below this height
                    // Key is greater than purgeKeyStart and less than purgeKeyEnd
                    var gt = TransientStoreQueryParams.GreaterThan(key, purgeKeyStart);
                    var lt = TransientStoreQueryParams.LessThan(key, purgeKeyEnd);
                    if (!(gt && lt))
                    {
                        // Ignore keys outside the range
                        continue;
                    }

                    // We're in the right range
                    // Explode key
                    (uint256 txId, Guid uuid, uint blockHeight) = TransientStoreQueryParams.SplitCompositeKeyOfPurgeIndexByHeight(key);

                    // Remove data key value
                    var dataKey = TransientStoreQueryParams.CreateCompositeKeyForPvtRWSet(blockHeight, txId, uuid);
                    tx.RemoveKey(Table, dataKey, (object)null);

                    // Remove block height purge key
                    var purgeKey = TransientStoreQueryParams.CreateCompositeKeyForPurgeIndexByHeight(blockHeight, txId, uuid);
                    tx.RemoveKey(Table, purgeKey, (object)null);

                    // TODO Remove tx purge key when it's implemented.
                }

                // Update min block height.
                tx.Insert(Table, MinBlockHeightKey, height);

                // Commit
                tx.Commit();
            }
        }
    }

    public static class TransientStoreQueryParams
    {
        public static byte[] CompositeKeySeparator = { 0x00 };
        public static byte[] PurgeIndexByHeightPrefix = BitConverter.GetBytes('H').Take(1).Reverse().ToArray();
        public static byte[] PrivateReadWriteSetPrefix = BitConverter.GetBytes('P').Take(1).Reverse().ToArray();

        /// <summary>
        /// Checks if byte array 1 is smaller than byte array 2. Arr1 &lt; Arr2
        /// </summary>
        /// <param name="arr1"></param>
        /// <param name="arr2"></param>
        /// <returns></returns>
        public static bool LessThan(byte[] arr1, byte[] arr2)
        {
            return LexicographicalCompare(arr1, arr2) < 0;
        }

        /// <summary>
        /// Checks if byte array 1 is greater than byte array 2. Arr1 &gt; Arr2.
        /// </summary>
        /// <param name="arr1"></param>
        /// <param name="arr2"></param>
        /// <returns></returns>
        public static bool GreaterThan(byte[] arr1, byte[] arr2)
        {
            return LexicographicalCompare(arr1, arr2) > 0;
        }

        /// <summary>
        /// Checks if byte array 1 is equal to byte array 2. Arr1 == Arr2.
        /// </summary>
        /// <param name="arr1"></param>
        /// <param name="arr2"></param>
        /// <returns></returns>
        public static bool EqualTo(byte[] arr1, byte[] arr2)
        {
            return LexicographicalCompare(arr1, arr2) == 0;
        }

        public static int StructuralCompare(byte[] arr1, byte[] arr2)
        {
            var result = ((IStructuralComparable)arr1).CompareTo(arr2, Comparer<byte>.Default);
            return result;
        }

        private static int LexicographicalCompare<T>(IEnumerable<T> seq1, IEnumerable<T> seq2)
        {
            Comparer<T> comparator = Comparer<T>.Default;

            using (IEnumerator<T> seq1Enumerator = seq1.GetEnumerator())
            using (IEnumerator<T> seq2Enumerator = seq2.GetEnumerator())
            {
                for (; ; )
                {
                    var seq1Next = seq1Enumerator.MoveNext();
                    var seq2Next = seq2Enumerator.MoveNext();

                    // If seq2 is longer than seq1, return -1, otherwise return 1
                    if (seq1Next != seq2Next)
                        return seq2Next ? -1 : 1;

                    // We've reached the end of both and not found a difference.
                    if (!seq1Next)
                        return 0;

                    // Compare the elements and return the difference if there is one.
                    int diff = comparator.Compare(seq1Enumerator.Current, seq2Enumerator.Current);
                    if (diff != 0)
                        return diff;
                }
            }
        }

        public static byte[] CreateCompositeKeyForPvtRWSet(uint height, uint256 txId, Guid guid)
        {
            var compositeKey = new byte[0];
            compositeKey = compositeKey.Combine(TransientStoreQueryParams.PrivateReadWriteSetPrefix);
            compositeKey = compositeKey.Combine(TransientStoreQueryParams.CompositeKeySeparator);
            compositeKey = compositeKey.Combine(CreateCompositeKeyWithoutPrefixForTxid(height, txId, guid));

            return compositeKey;
        }

        public static (uint256 txId, Guid uuid, uint blockHeight) SplitCompositeKeyForPvtRWSet(byte[] key)
        {
            var withoutPrefix = key.Skip(PrivateReadWriteSetPrefix.Length + CompositeKeySeparator.Length).ToArray();

            return SplitCompositeKeyWithoutPrefixForTxid(withoutPrefix);
        }

        private static byte[] CreateCompositeKeyWithoutPrefixForTxid(uint height, uint256 txId, Guid guid)
        {
            var compositeKey = new byte[0];
            compositeKey = compositeKey.Combine(txId.ToBytes(true));
            compositeKey = compositeKey.Combine(TransientStoreQueryParams.CompositeKeySeparator);
            compositeKey = compositeKey.Combine(guid.ToByteArray().Reverse().ToArray());
            compositeKey = compositeKey.Combine(TransientStoreQueryParams.CompositeKeySeparator);
            compositeKey = compositeKey.Combine(BitConverter.GetBytes(height).Reverse().ToArray());

            return compositeKey;
        }

        private static (uint256 txId, Guid uuid, uint blockHeight) SplitCompositeKeyWithoutPrefixForTxid(byte[] key)
        {
            var txIdBytes = key.Take(32).ToArray();
            var guidBytes = key.Skip(32 + CompositeKeySeparator.Length).Take(16).Reverse().ToArray();
            var heightBytes = key.Skip(32 + 16 + 2 * CompositeKeySeparator.Length).Take(sizeof(uint)).Reverse().ToArray();

            return (new uint256(txIdBytes), new Guid(guidBytes), BitConverter.ToUInt32(heightBytes));
        }


        public static byte[] CreateCompositeKeyForPurgeIndexByHeight(uint height, uint256 txId, Guid guid)
        {
            var compositeKey = new byte[0];
            compositeKey = compositeKey.Combine(TransientStoreQueryParams.PurgeIndexByHeightPrefix);
            compositeKey = compositeKey.Combine(TransientStoreQueryParams.CompositeKeySeparator);
            compositeKey = compositeKey.Combine(BitConverter.GetBytes(height).Reverse().ToArray());
            compositeKey = compositeKey.Combine(TransientStoreQueryParams.CompositeKeySeparator);
            compositeKey = compositeKey.Combine(txId.ToBytes(false));
            compositeKey = compositeKey.Combine(TransientStoreQueryParams.CompositeKeySeparator);
            compositeKey = compositeKey.Combine(guid.ToByteArray().Reverse().ToArray());

            return compositeKey;
        }

        public static (uint256 txId, Guid uuid, uint blockHeight) SplitCompositeKeyOfPurgeIndexByHeight(byte[] key)
        {
            // TODO fix this ugly mess
            var heightBytes = key.Skip(PurgeIndexByHeightPrefix.Length + CompositeKeySeparator.Length).Take(sizeof(uint)).Reverse().ToArray();
            var txIdBytes = key.Skip(PurgeIndexByHeightPrefix.Length + CompositeKeySeparator.Length + sizeof(uint) + 1).Take(32).Reverse().ToArray();
            var guidBytes = key.Skip(PurgeIndexByHeightPrefix.Length + CompositeKeySeparator.Length + sizeof(uint) + 32 + 2).Take(16).Reverse().ToArray();

            return (new uint256(txIdBytes), new Guid(guidBytes), BitConverter.ToUInt32(heightBytes));
        }

        // TODO make key classes?
        public static byte[] CreatePurgeIndexByHeightRangeStartKey(uint height)
        {
            var startKey = new byte[0];
            startKey = startKey.Combine(TransientStoreQueryParams.PurgeIndexByHeightPrefix);
            startKey = startKey.Combine(TransientStoreQueryParams.CompositeKeySeparator);
            // TODO handle endianness nicer. It's pretty safe to assume all systems running this will be little-endian, but just in case...
            startKey = startKey.Combine(BitConverter.GetBytes(height).Reverse().ToArray());
            startKey = startKey.Combine(TransientStoreQueryParams.CompositeKeySeparator);

            return startKey;
        }

        public static byte[] CreatePurgeIndexByHeightRangeEndKey(uint height)
        {
            var endKey = new byte[0];
            endKey = endKey.Combine(TransientStoreQueryParams.PurgeIndexByHeightPrefix);
            endKey = endKey.Combine(TransientStoreQueryParams.CompositeKeySeparator);
            endKey = endKey.Combine(BitConverter.GetBytes(height).Reverse().ToArray());
            endKey = endKey.Combine(new byte[] { 0xFF });
            return endKey;
        }

        public static byte[] CreateTxIdRangeStartKey(uint256 txId)
        {
            var startKey = new byte[0];
            startKey = startKey.Combine(TransientStoreQueryParams.PrivateReadWriteSetPrefix);
            startKey = startKey.Combine(TransientStoreQueryParams.CompositeKeySeparator);
            startKey = startKey.Combine(txId.ToBytes(true));
            startKey = startKey.Combine(TransientStoreQueryParams.CompositeKeySeparator);
            return startKey;
        }

        public static byte[] CreateTxIdRangeEndKey(uint256 txId)
        {
            var endKey = new byte[0];
            endKey = endKey.Combine(TransientStoreQueryParams.PrivateReadWriteSetPrefix);
            endKey = endKey.Combine(TransientStoreQueryParams.CompositeKeySeparator);
            endKey = endKey.Combine(txId.ToBytes(true));
            endKey = endKey.Combine(new byte[] { 0xFF });
            return endKey;
        }
    }

    public static class ByteArrayExtensions
    {
        public static byte[] Combine(this byte[] arr, byte[] other)
        {
            var result = new byte[arr.Length + other.Length];
            Array.Copy(arr, result, arr.Length);
            Array.Copy(other, 0, result, arr.Length, other.Length);

            return result;
        }
    }

    /// <summary>
    /// A composite key comprised of the txid, a uuid and block height.
    /// </summary>
    public struct TransientStoreKey
    {
        public readonly byte[] TxId;

        public readonly Guid UUID;

        public readonly uint BlockHeight;

        public TransientStoreKey(byte[] txId, Guid uuid, uint blockHeight)
        {
            this.TxId = new byte[txId.Length];
            Array.Copy(txId, this.TxId, txId.Length);

            this.UUID = uuid;
            this.BlockHeight = blockHeight;
        }

        public byte[] ToBytes()
        {
            var guid = this.UUID.ToByteArray();
            var result = new byte[this.TxId.Length + guid.Length + sizeof(uint)];
            Array.Copy(this.TxId, result, this.TxId.Length);
            Array.Copy(guid, 0, result, this.TxId.Length, guid.Length);
            Array.Copy(BitConverter.GetBytes(this.BlockHeight), 0, result, this.TxId.Length + guid.Length, sizeof(uint));

            return result;
        }
    }
    public struct CompositePurgeIndexKey
    {
        public byte[] PurgeHeightPrefix;
        public uint BlockHeight;

        public CompositePurgeIndexKey(uint blockHeight)
        {
            this.PurgeHeightPrefix = BitConverter.GetBytes('H').Take(1).ToArray();
            this.BlockHeight = blockHeight;
        }

        public byte[] ToBytes()
        {
            var blockHeight = BitConverter.GetBytes(this.BlockHeight);
            var separator = new byte[] { 0x00 };

            var result = new byte[this.PurgeHeightPrefix.Length + blockHeight.Length + separator.Length];
            Array.Copy(this.PurgeHeightPrefix, result, this.PurgeHeightPrefix.Length);
            Array.Copy(separator, 0, result, this.PurgeHeightPrefix.Length, separator.Length);
            Array.Copy(blockHeight, 0, result, this.PurgeHeightPrefix.Length + separator.Length, blockHeight.Length);

            return result;
        }
    }

    /// <summary>
    /// Contains a representation of private data for storage in the transient store.
    ///
    /// TODO the data represented here may change, but the final representation should always be as a byte array.
    /// </summary>
    public class TransientStorePrivateData
    {
        private readonly byte[] data;

        public TransientStorePrivateData(byte[] data)
        {
            this.data = new byte[data.Length];
            Array.Copy(data, this.data, data.Length);
        }

        public byte[] ToBytes()
        {
            var result = new byte[this.data.Length];
            Array.Copy(this.data, result, this.data.Length);

            return result;
        }
    }
}

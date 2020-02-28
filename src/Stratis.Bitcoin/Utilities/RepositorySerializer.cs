﻿using System;
using System.Collections.Generic;
using System.Linq;
using DBreeze.Utils;
using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
    public interface IRepositorySerializer
    {
        /// <summary>
        /// Serializes object to a binary data format.
        /// </summary>
        /// <param name="obj">Object to be serialized.</param>
        /// <returns>Binary data representing the serialized object.</returns>
        byte[] Serialize<T>(T obj);

        /// <summary>
        /// Deserializes binary data to an object of specific type.
        /// </summary>
        /// <param name="bytes">Binary data representing a serialized object.</param>
        /// <param name="type">Type of the serialized object.</param>
        /// <returns>Deserialized object.</returns>
        T Deserialize<T>(byte[] bytes);
    }

    /// <summary>
    /// Implementation of serialization and deserialization of objects that go into the DBreeze database.
    /// </summary>
    public class RepositorySerializer : IRepositorySerializer
    {
        private readonly ConsensusFactory consensusFactory;

        public RepositorySerializer(ConsensusFactory consensusFactory)
        {
            this.consensusFactory = consensusFactory;
        }

        /// <summary>
        /// Serializes object to a binary data format.
        /// </summary>
        /// <param name="obj">Object to be serialized.</param>
        /// <returns>Binary data representing the serialized object.</returns>
        public byte[] Serialize<T>(T obj)
        {
            if (typeof(T) == typeof(byte[]))
                return (byte[])(object)obj;

            if (obj == null)
                return new byte[] { };

            if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
                return new byte[] { (byte)((bool)(object)obj ? 1 : 0) };

            if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
            {
                byte[] bytes = BitConverter.GetBytes((int)(object)obj);
                if (BitConverter.IsLittleEndian)
                    bytes = bytes.Reverse().ToArray();
                return bytes;
            }

            if (typeof(T) == typeof(uint) || typeof(T) == typeof(uint?))
            {
                byte[] bytes = BitConverter.GetBytes((uint)(object)obj);
                if (BitConverter.IsLittleEndian)
                    bytes = bytes.Reverse().ToArray();
                return bytes;
            }

            Guard.Assert(!typeof(T).IsValueType);

            if (obj is IBitcoinSerializable serializable)
                return serializable.ToBytes(this.consensusFactory);

            if (obj is uint256 u256)
                return u256.ToBytes();

            if (obj is uint160 u160)
                return u160.ToBytes();

            if (obj is uint u32)
                return u32.ToBytes();

            if (obj is IEnumerable<object> collection)
            {
                object[] array = obj as object[] ?? collection.ToArray();

                var serializedItems = new byte[array.Length][];
                int itemIndex = 0;
                foreach (object arrayObject in array)
                {
                    byte[] serializedObject = this.Serialize(arrayObject);
                    serializedItems[itemIndex] = serializedObject;
                    itemIndex++;
                }

                return ConcatArrays(serializedItems);
            }

            throw new NotSupportedException();
        }

        /// <summary>
        /// Concatenates multiple byte arrays into a single byte array.
        /// </summary>
        /// <param name="arrays">Arrays to concatenate.</param>
        /// <returns>Concatenation of input arrays.</returns>
        /// <remarks>Based on https://stackoverflow.com/a/415396/3835864 .</remarks>
        private static byte[] ConcatArrays(byte[][] arrays)
        {
            var res = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, res, offset, array.Length);
                offset += array.Length;
            }

            return res;
        }

        public T Deserialize<T>(byte[] bytes)
        {
            if (bytes == null)
                return default;

            if (typeof(T) == typeof(byte[]))
                return (T)(object)bytes;

            if (bytes.Length == 0)
                return default;

            if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
                return (T)(object)(bytes[0] != 0);

            if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
            {
                var clonedBytes = (byte[])bytes.Clone();
                if (BitConverter.IsLittleEndian)
                    clonedBytes = clonedBytes.Reverse().ToArray();
                return (T)(object)BitConverter.ToInt32(clonedBytes, 0);
            }

            if (typeof(T) == typeof(uint) || typeof(T) == typeof(uint?))
            {
                var clonedBytes = (byte[])bytes.Clone();
                if (BitConverter.IsLittleEndian)
                    clonedBytes = clonedBytes.Reverse().ToArray();
                return (T)(object)BitConverter.ToUInt32(clonedBytes, 0);
            }

            Guard.Assert(!typeof(T).IsValueType);

            return (T)this.Deserialize(bytes, typeof(T));
        }

        /// <summary>
        /// Deserializes binary data to an object of specific type.
        /// </summary>
        /// <param name="bytes">Binary data representing a serialized object.</param>
        /// <param name="type">Type of the serialized object.</param>
        /// <returns>Deserialized object.</returns>
        private object Deserialize(byte[] bytes, Type type)
        {
            if (type == typeof(BlockHeader))
            {
                BlockHeader header = this.consensusFactory.CreateBlockHeader();
                header.ReadWrite(bytes, this.consensusFactory);
                return header;
            }

            if (type == typeof(Transaction))
            {
                Transaction transaction = this.consensusFactory.CreateTransaction();
                transaction.ReadWrite(bytes, this.consensusFactory);
                return transaction;
            }

            if (type == typeof(uint256))
                return new uint256(bytes);

            if (type == typeof(Block))
                return Block.Load(bytes, this.consensusFactory);

            if (type == typeof(BlockStake))
                return BlockStake.Load(bytes, this.consensusFactory);

            if (type == typeof(ProvenBlockHeader))
            {
                ProvenBlockHeader provenBlockHeader =
                    ((PosConsensusFactory)this.consensusFactory).CreateProvenBlockHeader();

                provenBlockHeader.ReadWrite(bytes, this.consensusFactory);
                return provenBlockHeader;
            }

            if (type == typeof(HashHeightPair))
                return HashHeightPair.Load(bytes, this.consensusFactory);

            if (typeof(IBitcoinSerializable).IsAssignableFrom(type))
            {
                var result = (IBitcoinSerializable)Activator.CreateInstance(type);
                result.ReadWrite(bytes, this.consensusFactory);
                return result;
            }

            throw new NotSupportedException();
        }
    }
}
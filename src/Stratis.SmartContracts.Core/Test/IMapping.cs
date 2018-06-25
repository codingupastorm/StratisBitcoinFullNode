using System;
using System.Collections.Generic;

namespace Stratis.SmartContracts.Core.Test
{
    public interface IMapping<T>
    {
        /// <summary>
        /// Store an item in the mapping at the given key. In PersistentState, the given item will be stored at {name}[{key}].
        /// </summary>
        void Put(string key, T value);

        /// <summary>
        /// Returns the item in the mapping at the given key. 
        /// </summary>
        T Get(string key);

        T this[string key] { get; set; }
    }

    public interface IList<T>
    {
        /// <summary>
        /// Store an item in the mapping at the given key. In PersistentState, the given item will be stored at {name}[{key}].
        /// </summary>
        void Put(int key, T value);

        /// <summary>
        /// Returns the item in the mapping at the given key. 
        /// </summary>
        T Get(int key);

        T this[int key] { get; set; }
    }

    public class Mapping<T> : IMapping<T>
    {
        private readonly IDictionary<string, object> mapping;

        private readonly string mappingName;

        public Mapping(IDictionary<string, object> mapping, string mappingName)
        {
            this.mapping = mapping;
            this.mappingName = mappingName;
        }

        public void Put(string key, T value)
        {
            if (typeof(T).IsGenericType)
            {
                Type wholeGenericType = typeof(T).GetGenericTypeDefinition();
                if (wholeGenericType == typeof(IMapping<>))
                {
                    throw new NotImplementedException();
                }
            }

            this.mapping[this.mappingName + "[" + key + "]"] = value;
        }

        public T Get(string key)
        {
            if (typeof(T).IsGenericType)
            {
                Type wholeGenericType = typeof(T).GetGenericTypeDefinition();
                if (wholeGenericType == typeof(IMapping<>))
                {
                    Type genericParam = typeof(T).GetGenericArguments()[0];
                    Type mappingType = typeof(Mapping<>);
                    Type genericMappingType = mappingType.MakeGenericType(genericParam);
                    return (T)Activator.CreateInstance(genericMappingType, new object[] { this.mapping, this.mappingName + "[" + key + "]" });
                }
            }

            return (T) this.mapping[this.mappingName + "[" + key + "]"];
        }

        public T this[string key]
        {
            get { return Get(key); }
            set { Put(key, value); }
        }
    }
}

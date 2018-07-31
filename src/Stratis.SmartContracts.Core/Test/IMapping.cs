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

    public interface IScList<T>
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

        int Count { get; }

        void Push(T value);
    }

    public class Mapping<T> : IMapping<T>
    {
        private readonly IDictionary<string, object> mapping;

        private readonly string name;

        public Mapping(IDictionary<string, object> mapping, string name)
        {
            this.mapping = mapping;
            this.name = name;
        }

        public void Put(string key, T value)
        {
            if (typeof(T).IsGenericType)
            {
                throw new NotSupportedException();
            }

            this.mapping[this.name + "[" + key + "]"] = value;
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
                    return (T)Activator.CreateInstance(genericMappingType, new object[] { this.mapping, this.name + "[" + key + "]" });
                }

                if (wholeGenericType == typeof(IScList<>))
                {
                    Type genericParam = typeof(T).GetGenericArguments()[0];
                    Type mappingType = typeof(ScList<>);
                    Type genericMappingType = mappingType.MakeGenericType(genericParam);
                    return (T)Activator.CreateInstance(genericMappingType, new object[] { this.mapping, this.name + "[" + key + "]" });
                }
            }

            return (T) this.mapping[this.name + "[" + key + "]"];
        }

        public T this[string key]
        {
            get { return Get(key); }
            set { Put(key, value); }
        }
    }

    public class ScList<T> : IScList<T>
    {
        private readonly IDictionary<string, object> mapping;

        private readonly string name;

        public ScList(IDictionary<string, object> mapping, string name)
        {
            this.mapping = mapping;
            this.name = name;
        }

        public int Count
        {
            get
            {
                if (this.mapping.ContainsKey(this.name + ".Count"))
                    return (int) this.mapping[this.name + ".Count"];

                return 0;
            } 
            set
            {
                this.mapping[this.name + ".Count"] = this.Count + 1;
            }
        }

        // TODO: this could be inefficient. Getting Count from db too many times.

        public void Push(T value)
        {
            Count++;
            Put(Count - 1, value);
        }

        public void Put(int key, T value)
        {
            if (key >= Count)
                throw new Exception("Key above list size.");

            if (typeof(T).IsGenericType)
            {
                throw new NotSupportedException();
            }

            this.mapping[this.name + "[" + key + "]"] = value;
        }

        public T Get(int key)
        {
            if (typeof(T).IsGenericType)
            {
                Type wholeGenericType = typeof(T).GetGenericTypeDefinition();
                if (wholeGenericType == typeof(IMapping<>))
                {
                    Type genericParam = typeof(T).GetGenericArguments()[0];
                    Type mappingType = typeof(Mapping<>);
                    Type genericMappingType = mappingType.MakeGenericType(genericParam);
                    return (T)Activator.CreateInstance(genericMappingType, new object[] { this.mapping, this.name + "[" + key + "]" });
                }
            }

            return (T)this.mapping[this.name + "[" + key + "]"];
        }

        public T this[int key]
        {
            get { return Get(key); }
            set { Put(key, value); }
        }
    }
}

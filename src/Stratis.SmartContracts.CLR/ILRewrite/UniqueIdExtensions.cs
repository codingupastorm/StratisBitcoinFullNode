using System;

namespace Stratis.SmartContracts.CLR.ILRewrite
{
    using System.Runtime.CompilerServices;

    public static class UniqueIdExtensions
    {
        private static readonly ConditionalWeakTable<object, RefId> _ids = new ConditionalWeakTable<object, RefId>();

        public static string GetRefId(this SmartContract obj)
        {
            if (obj == null)
                return default(Guid).ToString();

            return _ids.GetOrCreateValue(obj).Id.ToString();
        }

        private class RefId
        {
            public Guid Id { get; } = Guid.NewGuid();
        }
    }
}

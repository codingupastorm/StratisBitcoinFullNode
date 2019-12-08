using System.Collections.Generic;

namespace Stratis.Features.NodeStorage.Common
{
    /// <summary>
    /// Compares two byte arrays for equality.
    /// </summary>
    internal sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[] first, byte[] second)
        {
            if ((first?.Length ?? -1) != (second?.Length ?? -1))
                return false;

            for (int i = 0; i < (first?.Length ?? 0); i++)
                if (first[i] != second[i])
                    return false;

            return true;
        }

        public int GetHashCode(byte[] obj)
        {
            ulong hash = 17;

            foreach (byte objByte in obj)
            {
                hash = (hash << 5) - hash + objByte;
            }

            return (int)hash;
        }
    }
}

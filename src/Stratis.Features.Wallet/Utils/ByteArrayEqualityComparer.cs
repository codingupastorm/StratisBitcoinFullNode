using System.Collections.Generic;

namespace Stratis.Features.Wallet.Utils
{
    public class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
    {
        public int GetHashCode(byte[] obj)
        {
            ulong hash = 17;

            foreach (byte objByte in obj)
            {
                hash = (hash << 5) - hash + objByte;
            }

            return (int)hash;
        }

        public bool Equals(byte[] obj1, byte[] obj2)
        {
            if (obj1.Length != obj2.Length)
                return false;

            for (int i = 0; i < obj1.Length; i++)
                if (obj1[i] != obj2[i])
                    return false;

            return true;
        }
    }
}

using NBitcoin;
using TracerAttributes;

namespace Stratis.SmartContracts.Core.Util
{
    public static class ByteUtils
    {
        [NoTrace]
        public static bool TestFirstByte(Script script, byte opcode)
        {
            byte[] scriptBytes = script.ToBytes(true);

            if (scriptBytes.Length == 0)
                return false;

            return scriptBytes[0] == opcode;
        }
    }
}

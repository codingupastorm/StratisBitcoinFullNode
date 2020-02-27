using System.Collections.Generic;
using System.Linq;

namespace Stratis.SmartContracts.Core.ReadWrite
{
    public class ReadWriteSetDto
    {
        public List<ReadItemDto> Reads { get; set; }

        public List<WriteItemDto> Writes { get; set; }

        public ReadWriteSetDto(ReadWriteSet rws)
        {
            this.Reads = rws.ReadSet.ToList().Select(x => new ReadItemDto
            {
                ContractAddressHex = x.Key.ContractAddress.ToString(),
                KeyHex = x.Key.Key.ToHexString(),
                Version = x.Value
            }).ToList();

            this.Writes = rws.WriteSet.ToList().Select(x => new WriteItemDto
            {
                ContractAddressHex = x.Key.ContractAddress.ToString(),
                KeyHex = x.Key.Key.ToHexString(),
                ValueHex = x.Value.ToHexString()
            }).ToList();
        }
    }


    public class ReadItemDto
    {
        public string ContractAddressHex { get; set; }

        public string KeyHex { get; set; }

        public string Version { get; set; }
    }

    public class WriteItemDto
    {
        public string ContractAddressHex { get; set; }

        public string KeyHex { get; set; }

        public string ValueHex { get; set; }
    }
}

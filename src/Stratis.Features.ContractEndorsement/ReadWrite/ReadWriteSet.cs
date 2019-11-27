using System.Collections.Generic;

namespace Stratis.Features.ContractEndorsement.ReadWrite
{
    public class ReadWriteSet
    {
        public string ContractAddress { get; }
        public List<Read> Reads { get; }
        public List<Write> Writes { get; }

        public ReadWriteSet(string contractAddress)
        {
            this.ContractAddress = contractAddress;
            this.Reads = new List<Read>();
            this.Writes = new List<Write>();
        }
    }


}

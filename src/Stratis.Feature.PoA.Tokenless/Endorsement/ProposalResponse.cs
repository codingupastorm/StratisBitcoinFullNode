using System;
using System.Collections.Generic;
using System.Text;
using HashLib;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public class SignedProposalResponse : IBitcoinSerializable
    {
        public SignedProposalResponse()
        {
            this.Signatures = new List<byte[]>();
        }

        public ProposalResponse ProposalResponse { get; set; }

        public List<byte[]> Signatures { get; set; }

        public byte[] ToBytes()
        {
            return Encoding.UTF8.GetBytes(JsonConvert.ToString(this));
        }

        public void ReadWrite(BitcoinStream stream)
        {
            var serialized = this.ToBytes();
            stream.ReadWrite(ref serialized);
        }
    }
    
    public class ProposalResponse
    {
        public ReadWriteSet ReadWriteSet { get; set; }

        public byte[] ToBytes()
        {
            return this.ReadWriteSet.ToJsonEncodedBytes();
        }

        public static ProposalResponse FromBytes(byte[] data)
        {
            return new ProposalResponse
            {
                ReadWriteSet = ReadWriteSet.FromJsonEncodedBytes(data)
            };
        }

        public uint256 GetHash()
        {
            return new uint256(HashFactory.Crypto.SHA3.CreateKeccak256().ComputeBytes(this.ToBytes()).GetBytes());
        }
    }
}

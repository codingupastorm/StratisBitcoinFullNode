using System;
using System.Collections.Generic;
using System.Text;
using HashLib;
using NBitcoin;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
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

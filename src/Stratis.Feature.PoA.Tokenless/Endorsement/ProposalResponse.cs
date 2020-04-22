﻿using System.Text;
using HashLib;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.SmartContracts.Core.ReadWrite;

namespace Stratis.Feature.PoA.Tokenless.Endorsement
{
    public class SignedProposalResponse
    {
        public ProposalResponse ProposalResponse { get; set; }

        public Endorsement Endorsement { get; set; }

        public byte[] ToBytes()
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this));
        }

        public static SignedProposalResponse FromBytes(byte[] bytes)
        {
            return JsonConvert.DeserializeObject<SignedProposalResponse>(Encoding.UTF8.GetString(bytes));
        }
    }

    public class Endorsement
    {
        public Endorsement(byte[] signature, byte[] pubKey)
        {
            this.Signature = signature;
            this.PubKey = pubKey;
        }

        public byte[] Signature { get; }

        public byte[] PubKey { get; }
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

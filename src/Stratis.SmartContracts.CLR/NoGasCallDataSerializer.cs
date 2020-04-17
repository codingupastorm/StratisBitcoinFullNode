using System;
using System.Collections.Generic;
using System.Linq;
using CSharpFunctionalExtensions;
using NBitcoin;
using Nethereum.RLP;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Serializes smart contracts without gas or vm version data.
    ///
    /// For CREATE:
    /// - CREATE opcode
    /// - SmartContract bytecode
    /// - Method parameters
    /// - Contract config
    ///
    /// For CALL:
    /// - CALL opcode
    /// - Contract Address
    /// - Contract method name
    /// - Method parameters
    /// </summary>
    public class NoGasCallDataSerializer : CallDataSerializer
    {
        public const int VmVersionToSet = 0;
        public const ulong GasPriceToSet = 0;
        public const ulong GasLimitToSet = 100_000;

        private readonly IMethodParameterSerializer methodParamSerializer;
        private readonly IContractPrimitiveSerializer primitiveSerializer;

        public NoGasCallDataSerializer(IContractPrimitiveSerializer primitiveSerializer) : base(primitiveSerializer)
        {
            this.primitiveSerializer = primitiveSerializer;
            this.methodParamSerializer = new MethodParameterByteSerializer(primitiveSerializer);
        }

        public override Result<ContractTxData> Deserialize(byte[] smartContractBytes)
        {
            try
            {
                byte type = smartContractBytes[0];

                return IsCallContract(type)
                    ? this.DeserializeCallContract(smartContractBytes)
                    : this.DeserializeCreateContract(smartContractBytes);

            }
            catch (Exception e)
            {
                // TODO: Avoid this catch all exceptions
                return Result.Fail<ContractTxData>("Error deserializing calldata. " + e.Message);
            }
        }

        private Result<ContractTxData> DeserializeCreateContract(byte[] smartContractBytes)
        {
            byte[] remaining = smartContractBytes.Slice(OpcodeSize, (uint)(smartContractBytes.Length - OpcodeSize));

            IList<byte[]> decodedParams = RLPDecode(remaining);

            var contractExecutionCode = this.primitiveSerializer.Deserialize<byte[]>(decodedParams[0]);
            object[] methodParameters = this.DeserializeMethodParameters(decodedParams[1]);
            byte[] endorsementPolicy = decodedParams[2]; // TODO: Serialize in some useful way

            var callData = new ContractTxData(VmVersionToSet, GasPriceToSet, (Gas) GasLimitToSet, contractExecutionCode, endorsementPolicy, methodParameters);
            return Result.Ok(callData);
        }

        private Result<ContractTxData> DeserializeCallContract(byte[] smartContractBytes)
        {
            byte[] contractAddressBytes = smartContractBytes.Slice(OpcodeSize, AddressSize);
            var contractAddress = new uint160(contractAddressBytes);

            // Total size of the fixed-length parts
            uint prefix = OpcodeSize + AddressSize;
            byte[] remaining = smartContractBytes.Slice(prefix, (uint)(smartContractBytes.Length - prefix));

            IList<byte[]> decodedParams = RLPDecode(remaining);

            string methodName = this.primitiveSerializer.Deserialize<string>(decodedParams[0]);
            object[] methodParameters = this.DeserializeMethodParameters(decodedParams[1]);
            var callData = new ContractTxData(VmVersionToSet, GasPriceToSet, (Gas) GasLimitToSet, contractAddress, methodName, methodParameters);
            return Result.Ok(callData);
        }

        public override byte[] Serialize(ContractTxData contractTxData)
        {
            return IsCallContract(contractTxData.OpCodeType)
                ? this.SerializeCallContract(contractTxData)
                : this.SerializeCreateContract(contractTxData);
        }

        private byte[] SerializeCallContract(ContractTxData contractTxData)
        {
            var rlpBytes = new List<byte[]>();

            rlpBytes.Add(this.primitiveSerializer.Serialize(contractTxData.MethodName));

            this.AddMethodParams(rlpBytes, contractTxData.MethodParameters);

            byte[] encoded = RLP.EncodeList(rlpBytes.Select(RLP.EncodeElement).ToArray());

            var bytes = new byte[OpcodeSize + AddressSize + encoded.Length];

            contractTxData.ContractAddress.ToBytes().CopyTo(bytes, OpcodeSize);
            bytes[0] = contractTxData.OpCodeType;
            encoded.CopyTo(bytes, OpcodeSize + AddressSize);

            return bytes;
        }

        private byte[] SerializeCreateContract(ContractTxData contractTxData)
        {
            var rlpBytes = new List<byte[]>();

            rlpBytes.Add(contractTxData.ContractExecutionCode);

            this.AddPolicy(rlpBytes);

            base.AddMethodParams(rlpBytes, contractTxData.MethodParameters);

            byte[] encoded = RLP.EncodeList(rlpBytes.Select(RLP.EncodeElement).ToArray());

            var bytes = new byte[OpcodeSize + encoded.Length];
            bytes[0] = contractTxData.OpCodeType;
            encoded.CopyTo(bytes, OpcodeSize);

            return bytes;
        }

        private void AddPolicy(List<byte[]> rlpBytes)
        {
            // TODO: Add the policy to be serialized.
            rlpBytes.Add(new byte[0]);
        }
    }
}

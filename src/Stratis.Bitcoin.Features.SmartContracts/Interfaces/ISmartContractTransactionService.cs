﻿using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.Interfaces
{
    public interface ISmartContractTransactionService
    {
        EstimateFeeResult EstimateFee(ScTxFeeEstimateRequest request);
        BuildContractTransactionResult BuildTx(BuildContractTransactionRequest request);
        BuildCallContractTransactionResponse BuildCallTx(BuildCallContractTransactionRequest request);
        BuildCreateContractTransactionResponse BuildCreateTx(BuildCreateContractTransactionRequest request);
        ContractTxData BuildLocalCallTxData(LocalCallContractRequest request);
    }
}
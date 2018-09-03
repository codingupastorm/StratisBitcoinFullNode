﻿using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.SmartContracts.Networks;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;
using Stratis.SmartContracts.Executor.Reflection;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class SmartContractResultTransferProcessorTests
    {
        private readonly Network network;
        private readonly ILoggerFactory loggerFactory;
        private readonly SmartContractResultTransferProcessor transferProcessor;

        public SmartContractResultTransferProcessorTests()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
            this.network = new SmartContractsRegTest();
            this.transferProcessor = new SmartContractResultTransferProcessor(this.loggerFactory, this.network);
        }

        /*
         * The following are the possible scenarios going into condensing transaction creation:
         * 
         * 1) Contract has no balance, tx value = 0, transfer value = 0:
         *  DO NOTHING
         * 
         * 2) Contract has no balance, tx value > 0, transfer value = 0:
         *  ASSIGN CONTRACT CURRENT UTXO
         *  
         * 3) Contract has no balance, tx value = 0, transfer value > 0: 
         *  CAN'T HAPPEN
         *  
         * 4) Contract has no balance, tx value > 0, transfer value > 0:
         *  CREATE CONDENSING TX
         *  
         * 5) Contract has balance, tx value = 0, transfer value = 0:
         *  DO NOTHING
         *  
         * 6) Contract has balance, tx value > 0, transfer value = 0:
         *  CREATE CONDENSING TX
         *  
         * 7) Contract has balance, tx value = 0, transfer value > 0: 
         *  CREATE CONDENSING TX
         *  
         * 8) Contract has balance, tx value > 0, transfer value > 0
         *  CREATE CONDENSING TX
         *  
         */


        [Fact]
        public void NoBalance_TxValue0_TransferValue0()
        {
            uint160 contractAddress = new uint160(1);

            // No balance
            var stateMock = new Mock<IContractState>();
            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns<ContractUnspentOutput>(null);

            // No tx value
            var txContextMock = new Mock<ISmartContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(0);

            // No transfers
            var transfers = new List<TransferInfo>();

            var result = new SmartContractExecutionResult();

            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transfers, false);

            // Ensure no state changes were made and no transaction has been added
            Assert.Null(internalTransaction);
        }

        [Fact]
        public void NoBalance_TxValue1_TransferValue0()
        {
            uint160 contractAddress = new uint160(1);

            // No balance
            var stateMock = new Mock<IContractState>();
            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns<ContractUnspentOutput>(null);

            // 100 tx value
            var txContextMock = new Mock<ISmartContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(100);

            // No transfers
            var transfers = new List<TransferInfo>();

            var result = new SmartContractExecutionResult();

            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transfers, false);

            // Ensure unspent was saved, but no condensing transaction was generated.
            Assert.Null(internalTransaction);
            stateMock.Verify(x => x.SetUnspent(contractAddress, It.IsAny<ContractUnspentOutput>()));
        }

        [Fact]
        public void NoBalance_TxValue0_TransferValue1()
        {
            uint160 contractAddress = new uint160(1);
            uint160 receiverAddress = new uint160(2);

            // No balance
            var stateMock = new Mock<IContractState>();
            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns<ContractUnspentOutput>(null);

            // No tx value
            var txContextMock = new Mock<ISmartContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(0);

            // A transfer of 100
            var transferInfos = new List<TransferInfo>
            {
                new TransferInfo
                {
                    From = contractAddress,
                    To = receiverAddress,
                    Value = 100
                }
            };

            var result = new SmartContractExecutionResult();

            // This should be impossible - contract has no existing balance and didn't get sent anything so it cannot send value.
            // TODO: Could be more informative exception
            Assert.ThrowsAny<Exception>(() =>
            {
                Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transferInfos, false);
            });
        }

        [Fact]
        public void NoBalance_TxValue1_TransferValue1()
        {
            uint160 contractAddress = new uint160(1);
            uint160 receiverAddress = new uint160(2);

            // No balance
            var stateMock = new Mock<IContractState>();
            stateMock.Setup(x => x.GetUnspent(contractAddress)).Returns<ContractUnspentOutput>(null);

            // tx value 100
            var txContextMock = new Mock<ISmartContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(100);
            txContextMock.SetupGet(p => p.TransactionHash).Returns(new uint256(123));
            txContextMock.SetupGet(p => p.Nvout).Returns(1);

            // transfer 50
            var transferInfos = new List<TransferInfo>
            {
                new TransferInfo
                {
                    From = uint160.One,
                    To = new uint160(2),
                    Value = 50
                }
            };

            var result = new SmartContractExecutionResult();

            // Condensing tx generated. 1 input from tx and 2 outputs - 1 for each contract and receiver
            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, contractAddress, txContextMock.Object, transferInfos, false);
            Assert.NotNull(internalTransaction);
            Assert.Single(internalTransaction.Inputs);
            Assert.Equal(txContextMock.Object.TransactionHash, internalTransaction.Inputs[0].PrevOut.Hash);
            Assert.Equal(txContextMock.Object.Nvout, internalTransaction.Inputs[0].PrevOut.N);
            // TODO: Outputs
        }


        [Fact]
        public void Transfers_With_0Balance()
        {
            // Scenario where contract was not sent any funds, but did make a method call with value 0.
            var stateMock = new Mock<IContractState>();
            stateMock.Setup(x => x.GetCode(It.IsAny<uint160>())).Returns<byte[]>(null);
            var txContextMock = new Mock<ISmartContractTransactionContext>();
            txContextMock.SetupGet(p => p.TxOutValue).Returns(0);
            var result = new SmartContractExecutionResult();

            var transferInfos = new List<TransferInfo>
            {
                new TransferInfo
                {
                    From = uint160.One,
                    To = new uint160(2),
                    Value = 0
                }
            };

            Transaction internalTransaction = this.transferProcessor.Process(stateMock.Object, uint160.One, txContextMock.Object, transferInfos, false);

            // No condensing transaction was generated.
            Assert.Null(internalTransaction);
        }
    }
}

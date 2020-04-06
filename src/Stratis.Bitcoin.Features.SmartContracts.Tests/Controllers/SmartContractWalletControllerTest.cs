﻿using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Features.MemoryPool.Broadcasting;
using Stratis.Bitcoin.Features.SmartContracts.Interfaces;
using Stratis.Features.Wallet;
using Stratis.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests.Controllers
{
    public class SmartContractWalletControllerTest
    {
        private readonly Mock<IBroadcasterManager> broadcasterManager;
        private readonly Mock<ICallDataSerializer> callDataSerializer;
        private readonly Mock<IConnectionManager> connectionManager;
        private readonly Mock<ILoggerFactory> loggerFactory;
        private readonly Network network;
        private readonly Mock<IReceiptRepository> receiptRepository;
        private readonly Mock<IWalletManager> walletManager;
        private Mock<ISmartContractTransactionService> smartContractTransactionService;

        public SmartContractWalletControllerTest()
        {
            this.broadcasterManager = new Mock<IBroadcasterManager>();
            this.callDataSerializer = new Mock<ICallDataSerializer>();
            this.connectionManager = new Mock<IConnectionManager>();
            this.loggerFactory = new Mock<ILoggerFactory>();
            this.network = new SmartContractsRegTest();
            this.receiptRepository = new Mock<IReceiptRepository>();
            this.walletManager = new Mock<IWalletManager>();
            this.smartContractTransactionService = new Mock<ISmartContractTransactionService>();
        }
    }
}

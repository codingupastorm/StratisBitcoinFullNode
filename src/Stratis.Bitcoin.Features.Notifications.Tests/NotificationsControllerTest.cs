﻿using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Core.AsyncWork;
using Stratis.Core.Networks;
using Stratis.Core.Signals;
using Stratis.Core.Utilities;
using Stratis.Core.Utilities.JsonErrors;
using Stratis.Features.Notifications.Controllers;
using Xunit;

namespace Stratis.Features.Notifications.Tests
{
    public class NotificationsControllerTest : LogsTestBase
    {
        private readonly Network network;

        public NotificationsControllerTest()
        {
            this.network = new StratisMain();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [Trait("Module", "NotificationsController")]
        public void Given_SyncActionIsCalled_When_QueryParameterIsNullOrEmpty_Then_ReturnBadRequest(string from)
        {
            var chain = new Mock<ChainIndexer>();

            var loggerFactory = new Mock<LoggerFactory>();
            var signals = new Signals(loggerFactory.Object, null);
            var nodeLifetime = new NodeLifetime();
            var asyncProvider = new AsyncProvider(loggerFactory.Object, signals, nodeLifetime);
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, asyncProvider, nodeLifetime);

            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);
            IActionResult result = notificationController.SyncFrom(from);

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_ABlockHeightIsSpecified_Then_TheChainIsSyncedFromTheHash()
        {
            // Set up
            int heightLocation = 480946;
            string hashLocation = "000000000000000000c03dbe6ee5fedb25877a12e32aa95bc1d3bd480d7a93f9";
            uint256 hash = uint256.Parse(hashLocation);

            var chainedHeader = new ChainedHeader(this.network.Consensus.ConsensusFactory.CreateBlockHeader(), hash, null);
            var chain = new Mock<ChainIndexer>();
            chain.Setup(c => c.GetHeader(heightLocation)).Returns(chainedHeader);

            var loggerFactory = new Mock<LoggerFactory>();
            var signals = new Signals(loggerFactory.Object, null);
            var nodeLifetime = new NodeLifetime();
            var asyncProvider = new AsyncProvider(loggerFactory.Object, signals, nodeLifetime);
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, asyncProvider, nodeLifetime);

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);
            IActionResult result = notificationController.SyncFrom(heightLocation.ToString());

            // Assert
            chain.Verify(c => c.GetHeader(heightLocation), Times.Once);
            blockNotification.Verify(b => b.SyncFrom(hash), Times.Once);
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_ABlockHashIsSpecified_Then_TheChainIsSyncedFromTheHash()
        {
            // Set up
            int heightLocation = 480946;
            string hashLocation = "000000000000000000c03dbe6ee5fedb25877a12e32aa95bc1d3bd480d7a93f9";
            uint256 hash = uint256.Parse(hashLocation);

            var chainedHeader = new ChainedHeader(this.network.Consensus.ConsensusFactory.CreateBlockHeader(), hash, null);
            var chain = new Mock<ChainIndexer>();
            chain.Setup(c => c.GetHeader(uint256.Parse(hashLocation))).Returns(chainedHeader);

            var loggerFactory = new Mock<LoggerFactory>();
            var signals = new Signals(loggerFactory.Object, null);
            var nodeLifetime = new NodeLifetime();
            var asyncProvider = new AsyncProvider(loggerFactory.Object, signals, nodeLifetime);
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, asyncProvider, nodeLifetime);

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);
            IActionResult result = notificationController.SyncFrom(hashLocation);

            // Assert
            chain.Verify(c => c.GetHeader(heightLocation), Times.Never);
            blockNotification.Verify(b => b.SyncFrom(hash), Times.Once);
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_ANonExistingBlockHashIsSpecified_Then_ABadRequestErrorIsReturned()
        {
            // Set up
            string hashLocation = "000000000000000000c03dbe6ee5fedb25877a12e32aa95bc1d3bd480d7a93f9";

            var chain = new Mock<ChainIndexer>();
            chain.Setup(c => c.GetHeader(uint256.Parse(hashLocation))).Returns((ChainedHeader)null);

            var loggerFactory = new Mock<LoggerFactory>();
            var signals = new Signals(loggerFactory.Object, null);
            var nodeLifetime = new NodeLifetime();
            var asyncProvider = new AsyncProvider(loggerFactory.Object, signals, nodeLifetime);
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, asyncProvider, nodeLifetime);

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);

            // Assert
            IActionResult result = notificationController.SyncFrom(hashLocation);

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_AnInvalidBlockHashIsSpecified_Then_AnExceptionIsThrown()
        {
            // Set up
            string hashLocation = "notAValidHash";
            var chain = new Mock<ChainIndexer>();

            var loggerFactory = new Mock<LoggerFactory>();
            var signals = new Signals(loggerFactory.Object, null);
            var nodeLifetime = new NodeLifetime();
            var asyncProvider = new AsyncProvider(loggerFactory.Object, signals, nodeLifetime);
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, asyncProvider, nodeLifetime);

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);

            // Assert
            Assert.Throws<FormatException>(() => notificationController.SyncFrom(hashLocation));
        }

        [Fact]
        public void Given_SyncActionIsCalled_When_HeightNotOnChain_Then_ABadRequestErrorIsReturned()
        {
            // Set up
            var chain = new Mock<ChainIndexer>();
            chain.Setup(c => c.GetHeader(15)).Returns((ChainedHeader)null);

            var loggerFactory = new Mock<LoggerFactory>();
            var signals = new Signals(loggerFactory.Object, null);
            var nodeLifetime = new NodeLifetime();
            var asyncProvider = new AsyncProvider(loggerFactory.Object, signals, nodeLifetime);
            var blockNotification = new Mock<BlockNotification>(this.LoggerFactory.Object, chain.Object, asyncProvider, nodeLifetime);

            // Act
            var notificationController = new NotificationsController(blockNotification.Object, chain.Object);

            // Assert
            IActionResult result = notificationController.SyncFrom("15");

            var errorResult = Assert.IsType<ErrorResult>(result);
            var errorResponse = Assert.IsType<ErrorResponse>(errorResult.Value);
            Assert.Single(errorResponse.Errors);

            ErrorModel error = errorResponse.Errors[0];
            Assert.Equal(400, error.Status);
        }
    }
}

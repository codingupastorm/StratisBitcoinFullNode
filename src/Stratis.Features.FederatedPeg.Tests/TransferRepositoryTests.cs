using System.Collections.Generic;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.Models;
using Stratis.Features.FederatedPeg.SourceChain;
using Stratis.Features.FederatedPeg.TargetChain;
using Stratis.Sidechains.Networks.CirrusV2;
using Xunit;

namespace Stratis.Features.FederatedPeg.Tests
{
    public class TransferRepositoryTests
    {
        private readonly TransferRepository transferRepository;

        public TransferRepositoryTests()
        {
            Network network = CirrusNetwork.NetworksSelector.Regtest();
            var dbreezeSerializer = new DBreezeSerializer(network);
            DataFolder dataFolder = TestBase.CreateDataFolder(this);
            Mock<IFederationGatewaySettings> settings = new Mock<IFederationGatewaySettings>();
            settings.Setup(x => x.MultiSigAddress)
                .Returns(new BitcoinPubKeyAddress(new KeyId(), network));

            this.transferRepository = new TransferRepository(dataFolder, settings.Object, dbreezeSerializer);
        }

        [Fact]
        public void StoreAndRetrieveDeposit()
        {
            var model = new MaturedBlockDepositsModel(new MaturedBlockInfoModel
            {
                BlockHeight = 0,
                BlockTime = 1234
            }, new List<IDeposit>
            {
                new Deposit(123, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 0, 123456)
            });
            var modelList = new List<MaturedBlockDepositsModel>
            {
                model
            };

            Assert.True(this.transferRepository.SaveDeposits(modelList));

            Transfer retrievedTransfer = this.transferRepository.GetTransfer(model.Deposits[0].Id);

            Assert.Equal(model.Deposits[0].Id, retrievedTransfer.DepositTransactionId);
            Assert.Equal(model.Deposits[0].Amount, (Money) retrievedTransfer.DepositAmount);
            Assert.Equal(model.Deposits[0].BlockNumber, retrievedTransfer.DepositHeight);
            Assert.Equal(model.BlockInfo.BlockTime, retrievedTransfer.DepositTime);
            Assert.Equal(model.Deposits[0].TargetAddress, retrievedTransfer.DepositTargetAddress);
        }

        [Fact]
        public void MustSyncFromZero()
        {
            var model = new MaturedBlockDepositsModel(new MaturedBlockInfoModel
            {
                BlockHeight = 1,
                BlockTime = 1234
            }, new List<IDeposit>
            {
                new Deposit(123, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 1, 123456)
            });
            var modelList = new List<MaturedBlockDepositsModel>
            {
                model
            };

            Assert.False(this.transferRepository.SaveDeposits(modelList));

            Transfer retrievedTransfer = this.transferRepository.GetTransfer(model.Deposits[0].Id);
            Assert.Null(retrievedTransfer);
        }

        [Fact]
        public void CanSaveMultipleDepositsInOneBlock()
        {
            var modelList = new List<MaturedBlockDepositsModel>
            {
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 0,
                    BlockTime = 1234
                }, new List<IDeposit>
                {
                    new Deposit(123, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 0, 123456),
                    new Deposit(1234, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 0, 123456)
                })
            };


            Assert.True(this.transferRepository.SaveDeposits(modelList));

            foreach (IDeposit deposit in modelList[0].Deposits)
            {
                Transfer retrievedDeposit = this.transferRepository.GetTransfer(deposit.Id);

                Assert.Equal(deposit.Id, retrievedDeposit.DepositTransactionId);
                Assert.Equal(deposit.Amount, (Money) retrievedDeposit.DepositAmount);
                Assert.Equal(deposit.BlockNumber, retrievedDeposit.DepositHeight);
                Assert.Equal(deposit.TargetAddress, retrievedDeposit.DepositTargetAddress);
            }
        }

        [Fact]
        public void CanSaveMultipleBlocks()
        {
            var modelList = new List<MaturedBlockDepositsModel>
            {
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 0,
                    BlockTime = 1234
                }, new List<IDeposit>
                {
                    new Deposit(123, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 0, 123456)
                }),
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 1,
                    BlockTime = 1235
                }, new List<IDeposit>
                {
                    new Deposit(1234, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 1, 123456)
                }),
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 2,
                    BlockTime = 1236
                }, new List<IDeposit>
                {
                    new Deposit(12345, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 2, 123456)
                })
            };


            Assert.True(this.transferRepository.SaveDeposits(modelList));

            foreach (MaturedBlockDepositsModel maturedBlockDeposit in modelList)
            {
                Transfer retrievedTransfer = this.transferRepository.GetTransfer(maturedBlockDeposit.Deposits[0].Id);

                Assert.Equal(maturedBlockDeposit.Deposits[0].Id, retrievedTransfer.DepositTransactionId);
                Assert.Equal(maturedBlockDeposit.Deposits[0].Amount, (Money) retrievedTransfer.DepositAmount);
                Assert.Equal(maturedBlockDeposit.Deposits[0].BlockNumber, retrievedTransfer.DepositHeight);
                Assert.Equal(maturedBlockDeposit.Deposits[0].TargetAddress, retrievedTransfer.DepositTargetAddress);
            }
        }

        [Fact]
        public void CantSkipBlocks()
        {
            var modelList = new List<MaturedBlockDepositsModel>
            {
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 0,
                    BlockTime = 1234
                }, new List<IDeposit>
                {
                    new Deposit(123, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 0, 123456)
                }),
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 1,
                    BlockTime = 1235
                }, new List<IDeposit>
                {
                    new Deposit(1234, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 1, 123456)
                }),
                new MaturedBlockDepositsModel(new MaturedBlockInfoModel
                {
                    BlockHeight = 3,
                    BlockTime = 1236
                }, new List<IDeposit>
                {
                    new Deposit(12345, Money.Coins((decimal) 2.56), "mtXWDB6k5yC5v7TcwKZHB89SUp85yCKshy", 3, 123456)
                })
            };

            Assert.False(this.transferRepository.SaveDeposits(modelList));

            Transfer retrievedTransfer = this.transferRepository.GetTransfer(modelList[0].Deposits[0].Id);
            Assert.Null(retrievedTransfer);
        }
    }
}

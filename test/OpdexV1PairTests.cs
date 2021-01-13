using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexV1Contracts.Tests
{
    public class OpdexV1PairTests
    {
        private readonly Mock<ISmartContractState> _mockContractState;
        private readonly Mock<IContractLogger> _mockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> _mockInternalExecutor;
        private readonly Address _router;
        private readonly Address _pair;
        private readonly Address _feeToSetter;
        private readonly Address _feeTo;
        private readonly Address _token;
        private readonly Address _trader0;
        private readonly Address _trader1;

        public OpdexV1PairTests()
        {
            var mockPersistentState = new Mock<IPersistentState>();

            _mockContractLogger = new Mock<IContractLogger>();
            _mockContractState = new Mock<ISmartContractState>();
            _mockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            _mockContractState.Setup(x => x.PersistentState).Returns(mockPersistentState.Object);
            _mockContractState.Setup(x => x.ContractLogger).Returns(_mockContractLogger.Object);
            _mockContractState.Setup(x => x.InternalTransactionExecutor).Returns(_mockInternalExecutor.Object);
            _router = "0x0000000000000000000000000000000000000001".HexToAddress();
            _pair = "0x0000000000000000000000000000000000000002".HexToAddress();
            _feeToSetter = "0x0000000000000000000000000000000000000003".HexToAddress();
            _feeTo = "0x0000000000000000000000000000000000000004".HexToAddress();
            _token = "0x0000000000000000000000000000000000000005".HexToAddress();
            _trader0 = "0x0000000000000000000000000000000000000006".HexToAddress();
            _trader1 = "0x0000000000000000000000000000000000000007".HexToAddress();
        }

        private OpdexV1Pair CreateNewOpdexPair()
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(_pair, _router, 0));
            _mockContractState.Setup(x => x.PersistentState.GetAddress("Token")).Returns(_token);
            _mockContractState.Setup(x => x.PersistentState.GetAddress("Factory")).Returns(_router);

            return new OpdexV1Pair(_mockContractState.Object, _token);
        }

        [Fact]
        public void CreatesNewPair_Success()
        {
            var pair = CreateNewOpdexPair();

            _mockContractState.Verify(x => x.PersistentState.SetAddress("Token", _token), Times.Once);
            _mockContractState.Verify(x => x.PersistentState.SetAddress("Factory", _router), Times.Once);
        }

        [Fact]
        public void GetBalance_Success()
        {
            const ulong expected = 100;
            _mockContractState.Setup(x => x.PersistentState.GetUInt64($"Balance:{_trader0}")).Returns(expected);

            var pair = CreateNewOpdexPair();

            var balance = pair.GetBalance(_trader0);

            balance.Should().Be(expected);
        }

        [Fact]
        public void GetAllowance_Success()
        {
            const ulong expected = 100;
            _mockContractState.Setup(x => x.PersistentState.GetUInt64($"Allowance:{_trader0}:{_trader1}")).Returns(expected);

            var pair = CreateNewOpdexPair();

            var allowance = pair.GetAllowance(_trader0, _trader1);

            allowance.Should().Be(expected);
        }

        [Fact]
        public void GetReserves_Success()
        {
            const ulong expectedCrs = 100;
            const ulong expectedToken = 150;

            _mockContractState.Setup(x => x.PersistentState.GetUInt64("ReserveCrs")).Returns(expectedCrs);
            _mockContractState.Setup(x => x.PersistentState.GetUInt64("ReserveToken")).Returns(expectedToken);

            var pair = CreateNewOpdexPair();

            var reserves = pair.GetReserves();

            reserves[0].Should().Be(expectedCrs);
            reserves[1].Should().Be(expectedToken);
        }

        [Fact]
        public void Sync_Success()
        {
            const ulong expectedBalanceCrs = 100;
            const ulong expectedBalanceToken = 150;
            var expectedLog = new OpdexV1Pair.SyncEvent {ReserveCrs = expectedBalanceCrs, ReserveToken = expectedBalanceToken};

            var expectedSrcBalanceParams = new object[] {_pair};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _token, 0ul, "GetBalance", expectedSrcBalanceParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedBalanceToken));

            _mockContractState.Setup(x => x.GetBalance).Returns(() => expectedBalanceCrs);

            var pair = CreateNewOpdexPair();

            pair.Sync();

            _mockInternalExecutor.Verify(x => x.Call(_mockContractState.Object, _token, 0ul, "GetBalance", expectedSrcBalanceParams, 0ul), Times.Once);
            _mockContractState.Verify(x => x.PersistentState.SetUInt64("ReserveCrs", expectedBalanceCrs), Times.Once);
            _mockContractState.Verify(x => x.PersistentState.SetUInt64("ReserveToken", expectedBalanceToken), Times.Once);
            _mockContractLogger.Verify(x => x.Log(_mockContractState.Object, expectedLog), Times.Once);
        }

        [Fact]
        public void Skim_Success()
        {
            const ulong expectedBalanceCrs = 100;
            const ulong expectedBalanceToken = 150;
            const ulong currentReserveCrs = 50;
            const ulong currentReserveToken = 100;

            _mockContractState.Setup(x => x.PersistentState.GetUInt64("ReserveCrs")).Returns(currentReserveCrs);
            _mockContractState.Setup(x => x.PersistentState.GetUInt64("ReserveToken")).Returns(currentReserveToken);

            var expectedSrcBalanceParams = new object[] {_pair};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _token, 0ul, "GetBalance", expectedSrcBalanceParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedBalanceToken));

            var expectedTransferToParams = new object[] { _trader0, 50ul };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _token, 0ul, "TransferTo", expectedTransferToParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));

            _mockInternalExecutor
                .Setup(x => x.Transfer(_mockContractState.Object, _trader0, 50ul))
                .Returns(TransferResult.Transferred(true));

            _mockContractState.Setup(x => x.GetBalance).Returns(() => expectedBalanceCrs);

            var pair = CreateNewOpdexPair();

            pair.Skim(_trader0);

            _mockInternalExecutor.Verify(x => x.Call(_mockContractState.Object, _token, 0ul, "GetBalance", expectedSrcBalanceParams, 0ul), Times.Once);
            _mockInternalExecutor.Verify(x => x.Call(_mockContractState.Object, _token, 0ul, "TransferTo", expectedTransferToParams, 0ul), Times.Once);
            _mockInternalExecutor.Verify(x => x.Transfer(_mockContractState.Object, _trader0, 50ul), Times.Once);
        }
        
        #region Liquidity Pool Token Tests
        // Todo: Add expected failure tests

        [Fact]
        public void TransferTo_Success()
        {
            var pair = CreateNewOpdexPair();
            var from = _trader0;
            var to = _trader1;
            const ulong amount = 100;
            const ulong initialFromBalance = 200;
            const ulong initialToBalance = 0;
            const ulong finalFromBalance = 100;
            const ulong finalToBalance = 100;
         
            _mockContractState.Setup(x => x.PersistentState.GetUInt64($"Balance:{from}")).Returns(initialFromBalance);
            _mockContractState.Setup(x => x.PersistentState.GetUInt64($"Balance:{to}")).Returns(initialToBalance);
            _mockContractState.Setup(x => x.Message).Returns(new Message(_pair, from, 0));

            var success = pair.TransferTo(to, amount);

            _mockContractState.Verify(x => x.PersistentState.SetUInt64($"Balance:{from}", finalFromBalance), Times.Once);
            _mockContractState.Verify(x => x.PersistentState.SetUInt64($"Balance:{to}", finalToBalance), Times.Once);
            _mockContractLogger.Verify(x => x.Log(_mockContractState.Object, new OpdexV1Pair.TransferEvent { From = from, To = to, Amount = amount}), Times.Once);

            success.Should().BeTrue();
        }
        
        [Fact]
        public void TransferFrom_Success()
        {
            
        }

        [Fact]
        public void Approve_Success()
        {
            
        }
        
        #endregion
        
        #region Mint Tests

        [Fact]
        public void MintInitialLiquidity_Success()
        {
            
        }
        
        [Fact]
        // Todo: Finish this
        public void MintWithExistingReserves_Success()
        {
            const ulong currentReserveCrs = 5_000;
            const ulong currentReserveToken = 10_000;
            const ulong currentBalanceCrs = 5_500;
            const ulong currentBalanceToken = 11_000;
            const ulong currentTotalSupply = 2500;
            const ulong expectedLiquidity = 250;
            const ulong expectedKLast = 50_000_000;
            const ulong currentFeeToBalance = 100;
            const ulong currentTraderBalance = 0;
            const ulong mintedFee = 493; // Todo: Calculate and verify, I think expectedKLast is wrong ^

            _mockContractState.Setup(x => x.PersistentState.GetUInt64("ReserveCrs")).Returns(currentReserveCrs);
            _mockContractState.Setup(x => x.PersistentState.GetUInt64("ReserveToken")).Returns(currentReserveToken);
            _mockContractState.Setup(x => x.GetBalance).Returns(() => currentBalanceCrs);
            _mockContractState.Setup(x => x.PersistentState.GetUInt64("TotalSupply")).Returns(currentTotalSupply);
            _mockContractState.Setup(x => x.PersistentState.GetUInt64("KLast")).Returns(expectedKLast);
            _mockContractState.Setup(x => x.PersistentState.GetUInt64($"Balance:{_feeTo}")).Returns(currentFeeToBalance);
            _mockContractState.Setup(x => x.PersistentState.GetUInt64($"Balance:{_trader0}")).Returns(currentTraderBalance);

            var expectedSrcBalanceParams = new object[] {_pair};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _token, 0ul, "GetBalance", expectedSrcBalanceParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(currentBalanceToken));
            
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _router, 0ul, "GetFeeTo", null, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(_feeTo));
            
            var pair = CreateNewOpdexPair();

            var mintedLiquidity = pair.Mint(_trader0);

            mintedLiquidity.Should().Be(expectedLiquidity);
        }
        
        #endregion
        
        #region Burn Tests
        
        [Fact]
        public void Burn_Success()
        {
            
        }
        
        #endregion
        
        #region Swap Tests

        [Fact]
        public void SwapCRSForTokenSuccess()
        {
            const ulong swapAmountCrs = 500;
            const ulong currentReserveCrs = 5_500;
            const ulong currentReserveToken = 10_000;
            const ulong expectedReceivedToken = 997;
            
            _mockContractState.Setup(x => x.PersistentState.GetUInt64("ReserveCrs")).Returns(currentReserveCrs);
            _mockContractState.Setup(x => x.PersistentState.GetUInt64("ReserveToken")).Returns(currentReserveToken);
            
            var pair = CreateNewOpdexPair();
        }
        
        #endregion
    }
}
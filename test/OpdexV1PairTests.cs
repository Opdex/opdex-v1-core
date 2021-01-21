using System;
using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexV1Contracts.Tests
{
    public class OpdexV1PairTests : BaseContractTest
    {
        [Fact]
        public void GetsTokenProperties_Success()
        {
            var pair = CreateNewOpdexPair();

            pair.Decimals.Should().Be(8);
            pair.Name.Should().Be("Opdex Liquidity Pool Token");
            pair.Symbol.Should().Be("OLPT");
        }
        
        [Fact]
        public void CreatesNewPair_Success()
        {
            var pair = CreateNewOpdexPair();

            pair.Token.Should().Be(_token);
            pair.Controller.Should().Be(_controller);
        }

        [Fact]
        public void GetBalance_Success()
        {
            var expected = new UInt256(100);
            _persistentState.SetUInt256($"Balance:{_trader0}", expected);

            var pair = CreateNewOpdexPair();
            
            pair.GetBalance(_trader0).Should().Be(expected);
        }

        [Fact]
        public void GetAllowance_Success()
        {
            var expected = new UInt256(100);
            _persistentState.SetUInt256($"Allowance:{_trader0}:{_trader1}", expected);

            var pair = CreateNewOpdexPair();
            
            pair.GetAllowance(_trader0, _trader1).Should().Be(expected);
        }

        [Fact]
        public void GetReserves_Success()
        {
            var expectedCrs = new UInt256(100);
            var expectedToken = new UInt256(150);

            _persistentState.SetUInt256("ReserveCrs", expectedCrs);
            _persistentState.SetUInt256("ReserveToken", expectedToken);
            
            var pair = CreateNewOpdexPair();

            var reserves = pair.GetReserves();

            reserves[0].Should().Be(expectedCrs);
            reserves[1].Should().Be(expectedToken);
        }

        [Fact]
        public void Sync_Success()
        {
            var expectedBalanceCrs = new UInt256(100);
            var expectedBalanceToken = new UInt256(150);
            var expectedLog = new OpdexV1Pair.SyncEvent {ReserveCrs = expectedBalanceCrs, ReserveToken = expectedBalanceToken, EventType = nameof(OpdexV1Pair.SyncEvent)};

            var expectedSrcBalanceParams = new object[] {_pair};
            SetupCall(_token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceToken));

            SetupBalance(expectedBalanceCrs);

            var pair = CreateNewOpdexPair();

            pair.Sync();

            pair.ReserveCrs.Should().Be(expectedBalanceCrs);
            pair.ReserveToken.Should().Be(expectedBalanceToken);

            VerifyCall(_token, 0ul, "GetBalance", expectedSrcBalanceParams, Times.Once);
            VerifyLog(expectedLog, Times.Once);
        }

        [Fact]
        public void Skim_Success()
        {
            var expectedBalanceCrs = new UInt256(100);
            var expectedBalanceToken = new UInt256(150);
            var currentReserveCrs = new UInt256(50);
            var currentReserveToken = new UInt256(100);

            _persistentState.SetUInt256("ReserveCrs", currentReserveCrs);
            _persistentState.SetUInt256("ReserveToken", currentReserveToken);

            var expectedSrcBalanceParams = new object[] {_pair};
            SetupCall(_token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceToken));

            var expectedTransferToParams = new object[] { _trader0, new UInt256(50) };
            SetupCall(_token, 0ul, "TransferTo", expectedTransferToParams, TransferResult.Transferred(true));

            SetupTransfer(_trader0, 50ul, TransferResult.Transferred(true));

            SetupBalance(expectedBalanceCrs);

            var pair = CreateNewOpdexPair();

            pair.Skim(_trader0);

            VerifyCall(_token, 0ul, "GetBalance", expectedSrcBalanceParams, Times.Once);
            VerifyCall( _token, 0ul, "TransferTo", expectedTransferToParams, Times.Once);
            VerifyTransfer(_trader0, 50ul, Times.Once);
        }
        
        #region Liquidity Pool Token Tests

        [Fact]
        public void TransferTo_Success()
        {
            var pair = CreateNewOpdexPair();
            var from = _trader0;
            var to = _trader1;
            var amount = new UInt256(75);
            var initialFromBalance = new UInt256(200);
            var initialToBalance = new UInt256(25);
            var finalFromBalance = new UInt256(125);
            var finalToBalance = new UInt256(100);
            var expectedTransferEvent = new OpdexV1Pair.TransferEvent {From = from, To = to, Amount = amount, EventType = nameof(OpdexV1Pair.TransferEvent)};
         
            _persistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            _persistentState.SetUInt256($"Balance:{to}", initialToBalance);
            SetupMessage(_pair, from);

            var success = pair.TransferTo(to, amount);

            success.Should().BeTrue();
            pair.GetBalance(from).Should().Be(finalFromBalance);
            pair.GetBalance(to).Should().Be(finalToBalance);
            
            VerifyLog(expectedTransferEvent, Times.Once);
        }

        [Fact]
        public void TransferTo_Throws_InsufficientFromBalance()
        {
            var pair = CreateNewOpdexPair();
            var from = _trader0;
            var to = _trader1;
            var amount = new UInt256(115);
            var initialFromBalance = new UInt256(100);
            var initialToBalance = new UInt256(0);
         
            _persistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            _persistentState.SetUInt256($"Balance:{to}", initialToBalance);
            SetupMessage(_pair, from);
            
            pair
                .Invoking(p => p.TransferTo(to, amount))
                .Should().Throw<OverflowException>();
        }
        
        [Fact]
        public void TransferTo_Throws_ToBalanceOverflow()
        {
            var pair = CreateNewOpdexPair();
            var from = _trader0;
            var to = _trader1;
            var amount = new UInt256(1);
            var initialFromBalance = new UInt256(100);
            var initialToBalance = UInt256.MaxValue;
         
            _persistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            _persistentState.SetUInt256($"Balance:{to}", initialToBalance);
            SetupMessage(_pair, from);
            
            pair
                .Invoking(p => p.TransferTo(to, amount))
                .Should().Throw<OverflowException>();
        }
        
        [Fact]
        public void TransferFrom_Success()
        {
            var pair = CreateNewOpdexPair();
            var from = _trader0;
            var to = _trader1;
            var amount = new UInt256(30);
            var initialFromBalance = new UInt256(200);
            var initialToBalance = new UInt256(50);
            var initialSpenderAllowance = new UInt256(100);
            var finalFromBalance = new UInt256(170);
            var finalToBalance = new UInt256(80);
            var finalSpenderAllowance = new UInt256(70);
            var expectedTransferEvent = new OpdexV1Pair.TransferEvent {From = from, To = to, Amount = amount, EventType = nameof(OpdexV1Pair.TransferEvent)};
         
            _persistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            _persistentState.SetUInt256($"Balance:{to}", initialToBalance);
            _persistentState.SetUInt256($"Allowance:{from}:{to}", initialSpenderAllowance);
            SetupMessage(_pair, to);

            pair.TransferFrom(from, to, amount).Should().BeTrue();
            pair.GetBalance(from).Should().Be(finalFromBalance);
            pair.GetBalance(to).Should().Be(finalToBalance);
            pair.GetAllowance(from, to).Should().Be(finalSpenderAllowance);
            
            VerifyLog(expectedTransferEvent, Times.Once);
        }

        [Fact]
        public void TransferFrom_Throws_InsufficientFromBalance()
        {
            var pair = CreateNewOpdexPair();
            var from = _trader0;
            var to = _trader1;
            var amount = new UInt256(150);
            var initialFromBalance = new UInt256(100);
            var spenderAllowance = new UInt256(150);
         
            _persistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            _persistentState.SetUInt256($"Allowance:{from}:{to}", spenderAllowance);
            SetupMessage(_pair, to);
            
            pair
                .Invoking(p => p.TransferFrom(from, to, amount))
                .Should().Throw<OverflowException>();
        }
        
        [Fact]
        public void TransferFrom_Throws_InsufficientSpenderAllowance()
        {
            var pair = CreateNewOpdexPair();
            var from = _trader0;
            var to = _trader1;
            var amount = new UInt256(200);
            var initialFromBalance = new UInt256(1000);
            var spenderAllowance = new UInt256(150);
         
            _persistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            _persistentState.SetUInt256($"Allowance:{from}:{to}", spenderAllowance);
            SetupMessage(_pair, to);
            
            pair
                .Invoking(p => p.TransferFrom(from, to, amount))
                .Should().Throw<OverflowException>();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Approve_Success(bool isIStandardContractImplementation)
        {
            var pair = CreateNewOpdexPair();
            var from = _trader0;
            var spender = _trader1;
            var amount = new UInt256(100);
            var expectedApprovalEvent = new OpdexV1Pair.ApprovalEvent {Owner = from, Spender = spender, Amount = amount, EventType = nameof(OpdexV1Pair.ApprovalEvent)};
            
            SetupMessage(_pair, from);

            if (isIStandardContractImplementation)
            {
                var currentAmount = new UInt256(239872387492834); // doesn't matter, unused
                pair.Approve(spender, currentAmount, amount).Should().BeTrue();
            }
            else
            {
                pair.Approve(spender, amount).Should().BeTrue();
            }
            
            VerifyLog(expectedApprovalEvent, Times.Once);
        }
        
        #endregion
        
        #region Mint Tests

        [Fact]
        public void MintInitialLiquidity_Success()
        {
            var currentReserveCrs = new UInt256(0);
            var currentReserveToken = new UInt256(0);
            var currentBalanceCrs = new UInt256(100_000_000);
            var currentBalanceToken = new UInt256(1_900_000_000);
            var currentTotalSupply = new UInt256(0);
            var currentKLast = new UInt256(0);
            var currentFeeToBalance = new UInt256(0);
            var currentTraderBalance = new UInt256(0);
            var expectedLiquidity = new UInt256(435888894);

            SetupBalance(currentBalanceCrs);
            
            SetupBalance(currentBalanceCrs);
            _persistentState.SetUInt256("ReserveCrs",currentReserveCrs);
            _persistentState.SetUInt256("ReserveToken", currentReserveToken);
            _persistentState.SetUInt256("TotalSupply", currentTotalSupply);
            _persistentState.SetUInt256("KLast", currentKLast);
            _persistentState.SetUInt256($"Balance:{_feeTo}", currentFeeToBalance);
            _persistentState.SetUInt256($"Balance:{_trader0}", currentTraderBalance);
            
            var expectedSrcBalanceParams = new object[] {_pair};
            SetupCall(_token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));
            SetupCall(_controller, 0ul, "GetFeeTo", null, TransferResult.Transferred(_feeTo));
            
            var pair = CreateNewOpdexPair();

            var mintedLiquidity = pair.Mint(_trader0);

            mintedLiquidity.Should().Be(expectedLiquidity);
        }
        
        [Fact]
        // Todo: Finish this
        public void MintWithExistingReserves_Success()
        {
            var currentReserveCrs = new UInt256(5_000);
            var currentReserveToken = new UInt256(10_000);
            var currentBalanceCrs = new UInt256(5_500);
            var currentBalanceToken = new UInt256(11_000);
            var currentTotalSupply = new UInt256(2500);
            var expectedLiquidity = new UInt256(250);
            var expectedKLast = new UInt256(50_000_000);
            var currentFeeToBalance = new UInt256(100);
            var currentTraderBalance = new UInt256(0);
            var mintedFee = new UInt256(0); // Todo: Calculate and set

            SetupBalance(currentBalanceCrs);
            _persistentState.SetUInt256("ReserveCrs",currentReserveCrs);
            _persistentState.SetUInt256("ReserveToken", currentReserveToken);
            _persistentState.SetUInt256("TotalSupply", currentTotalSupply);
            _persistentState.SetUInt256("KLast", expectedKLast);
            _persistentState.SetUInt256($"Balance:{_feeTo}", currentFeeToBalance);
            _persistentState.SetUInt256($"Balance:{_trader0}", currentTraderBalance);

            var expectedSrcBalanceParams = new object[] {_pair};
            SetupCall(_token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));
            SetupCall(_controller, 0ul, "get_FeeTo", null, TransferResult.Transferred(_feeTo));
            
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
            var swapAmountCrs = new UInt256(500);
            var currentReserveCrs = new UInt256(5_500);
            var currentReserveToken = new UInt256(10_000);
            var expectedReceivedToken = new UInt256(997);
            
            _persistentState.SetUInt256("ReserveCrs", currentReserveCrs);
            _persistentState.SetUInt256("ReserveToken", currentReserveToken);
            
            var pair = CreateNewOpdexPair();
        }
        
        #endregion
    }
}
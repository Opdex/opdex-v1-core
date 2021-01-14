using System;
using FluentAssertions;
using Moq;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexV1Contracts.Tests
{
    public class OpdexV1PairTests : BaseContractTest
    {
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
            const ulong expected = 100;
            _persistentState.SetUInt64($"Balance:{_trader0}", expected);

            var pair = CreateNewOpdexPair();
            
            pair.GetBalance(_trader0).Should().Be(expected);
        }

        [Fact]
        public void GetAllowance_Success()
        {
            const ulong expected = 100;
            _persistentState.SetUInt64($"Allowance:{_trader0}:{_trader1}", expected);

            var pair = CreateNewOpdexPair();
            
            pair.GetAllowance(_trader0, _trader1).Should().Be(expected);
        }

        [Fact]
        public void GetReserves_Success()
        {
            const ulong expectedCrs = 100;
            const ulong expectedToken = 150;

            _persistentState.SetUInt64("ReserveCrs", expectedCrs);
            _persistentState.SetUInt64("ReserveToken", expectedToken);
            
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
            const ulong expectedBalanceCrs = 100;
            const ulong expectedBalanceToken = 150;
            const ulong currentReserveCrs = 50;
            const ulong currentReserveToken = 100;

            _persistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            _persistentState.SetUInt64("ReserveToken", currentReserveToken);

            var expectedSrcBalanceParams = new object[] {_pair};
            SetupCall(_token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceToken));

            var expectedTransferToParams = new object[] { _trader0, 50ul };
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
            const ulong amount = 75;
            const ulong initialFromBalance = 200;
            const ulong initialToBalance = 25;
            const ulong finalFromBalance = 125;
            const ulong finalToBalance = 100;
            var expectedTransferEvent = new OpdexV1Pair.TransferEvent {From = from, To = to, Amount = amount};
         
            _persistentState.SetUInt64($"Balance:{from}", initialFromBalance);
            _persistentState.SetUInt64($"Balance:{to}", initialToBalance);
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
            const ulong amount = 115;
            const ulong initialFromBalance = 100;
            const ulong initialToBalance = 0;
         
            _persistentState.SetUInt64($"Balance:{from}", initialFromBalance);
            _persistentState.SetUInt64($"Balance:{to}", initialToBalance);
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
            const ulong amount = 1;
            const ulong initialFromBalance = 100;
            const ulong initialToBalance = ulong.MaxValue;
         
            _persistentState.SetUInt64($"Balance:{from}", initialFromBalance);
            _persistentState.SetUInt64($"Balance:{to}", initialToBalance);
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
            const ulong amount = 30;
            const ulong initialFromBalance = 200;
            const ulong initialToBalance = 50;
            const ulong initialSpenderAllowance = 100;
            const ulong finalFromBalance = 170;
            const ulong finalToBalance = 80;
            const ulong finalSpenderAllowance = 70;
            var expectedTransferEvent = new OpdexV1Pair.TransferEvent {From = from, To = to, Amount = amount};
         
            _persistentState.SetUInt64($"Balance:{from}", initialFromBalance);
            _persistentState.SetUInt64($"Balance:{to}", initialToBalance);
            _persistentState.SetUInt64($"Allowance:{from}:{to}", initialSpenderAllowance);
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
            const ulong amount = 150;
            const ulong initialFromBalance = 100;
            const ulong spenderAllowance = 150;
         
            _persistentState.SetUInt64($"Balance:{from}", initialFromBalance);
            _persistentState.SetUInt64($"Allowance:{from}:{to}", spenderAllowance);
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
            const ulong amount = 200;
            const ulong initialFromBalance = 1000;
            const ulong spenderAllowance = 150;
         
            _persistentState.SetUInt64($"Balance:{from}", initialFromBalance);
            _persistentState.SetUInt64($"Allowance:{from}:{to}", spenderAllowance);
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
            const ulong amount = 100;
            var expectedTransferEvent = new OpdexV1Pair.ApprovalEvent {Owner = from, Spender = spender, Amount = amount};
            
            SetupMessage(_pair, from);

            if (isIStandardContractImplementation)
            {
                var currentAmount = 239872387492834ul; // doesn't matter, unused
                pair.Approve(spender, currentAmount, amount).Should().BeTrue();
            }
            else
            {
                pair.Approve(spender, amount).Should().BeTrue();
            }
            
            VerifyLog(expectedTransferEvent, Times.Once);
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

            SetupBalance(currentBalanceCrs);
            _persistentState.SetUInt64("ReserveCrs",currentReserveCrs);
            _persistentState.SetUInt64("ReserveToken", currentReserveToken);
            _persistentState.SetUInt64("TotalSupply", currentTotalSupply);
            _persistentState.SetUInt64("KLast", expectedKLast);
            _persistentState.SetUInt64($"Balance:{_feeTo}", currentFeeToBalance);
            _persistentState.SetUInt64($"Balance:{_trader0}", currentTraderBalance);

            var expectedSrcBalanceParams = new object[] {_pair};
            SetupCall(_token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));
            SetupCall(_controller, 0ul, "GetFeeTo", null, TransferResult.Transferred(_feeTo));
            
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
            
            _persistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            _persistentState.SetUInt64("ReserveToken", currentReserveToken);
            
            var pair = CreateNewOpdexPair();
        }
        
        #endregion
    }
}
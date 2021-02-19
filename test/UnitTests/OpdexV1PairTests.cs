using System;
using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexV1Contracts.Tests.UnitTests
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

            pair.Token.Should().Be(Token);
            pair.Controller.Should().Be(Controller);
            pair.StakeToken.Should().Be(StakeToken);
        }

        [Fact]
        public void GetBalance_Success()
        {
            UInt256 expected = 100;
            PersistentState.SetUInt256($"Balance:{Trader0}", expected);

            var pair = CreateNewOpdexPair();
            
            pair.GetBalance(Trader0).Should().Be(expected);
        }

        [Fact]
        public void GetAllowance_Success()
        {
            UInt256 expected = 100;
            PersistentState.SetUInt256($"Allowance:{Trader0}:{Trader1}", expected);

            var pair = CreateNewOpdexPair();
            
            pair.GetAllowance(Trader0, Trader1).Should().Be(expected);
        }

        [Fact]
        public void GetReserves_Success()
        {
            ulong expectedCrs = 100;
            UInt256 expectedToken = 150;

            PersistentState.SetUInt64("ReserveCrs", expectedCrs);
            PersistentState.SetUInt256("ReserveSrc", expectedToken);
            
            var pair = CreateNewOpdexPair();

            var reserves = pair.GetReserves();
            var reserveCrs = Serializer.ToUInt64(reserves[0]);
            var reserveToken = Serializer.ToUInt256(reserves[1]);
            
            reserveCrs.Should().Be(expectedCrs);
            reserveToken.Should().Be(expectedToken);
        }

        [Fact]
        public void Sync_Success()
        {
            const ulong expectedBalanceCrs = 100;
            UInt256 expectedBalanceToken = 150;
            var expectedLog = new SyncEvent {ReserveCrs = expectedBalanceCrs, ReserveSrc = expectedBalanceToken, EventTypeId = (byte)EventType.SyncEvent};

            var pair = CreateNewOpdexPair(expectedBalanceCrs);
            
            var expectedSrcBalanceParams = new object[] {Pair};
            SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceToken));

            pair.Sync();

            pair.ReserveCrs.Should().Be(expectedBalanceCrs);
            pair.ReserveSrc.Should().Be(expectedBalanceToken);

            VerifyCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, Times.Once);
            VerifyLog(expectedLog, Times.Once);
        }

        [Fact]
        public void Skim_Success()
        {
            const ulong expectedBalanceCrs = 100;
            UInt256 expectedBalanceToken = 150;
            UInt256 currentReserveCrs = 50;
            UInt256 currentReserveSrc = 100;

            var pair = CreateNewOpdexPair(expectedBalanceCrs);
            
            PersistentState.SetUInt64("ReserveCrs", (ulong)currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            var expectedSrcBalanceParams = new object[] {Pair};
            SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceToken));

            var expectedTransferToParams = new object[] { Trader0, (UInt256)50 };
            SetupCall(Token, 0ul, "TransferTo", expectedTransferToParams, TransferResult.Transferred(true));

            SetupTransfer(Trader0, 50ul, TransferResult.Transferred(true));
            
            pair.Skim(Trader0);

            VerifyCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, Times.Once);
            VerifyCall( Token, 0ul, "TransferTo", expectedTransferToParams, Times.Once);
            VerifyTransfer(Trader0, 50ul, Times.Once);
        }
        
        #region Liquidity Pool Token Tests

        [Fact]
        public void TransferTo_Success()
        {
            var pair = CreateNewOpdexPair();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 75;
            UInt256 initialFromBalance = 200;
            UInt256 initialToBalance = 25;
            UInt256 finalFromBalance = 125;
            UInt256 finalToBalance = 100;
            var expectedTransferEvent = new TransferEvent {From = from, To = to, Amount = amount, EventTypeId = (byte)EventType.TransferEvent};
         
            PersistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            PersistentState.SetUInt256($"Balance:{to}", initialToBalance);
            SetupMessage(Pair, from);

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
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 115;
            UInt256 initialFromBalance = 100;
            UInt256 initialToBalance = 0;
         
            PersistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            PersistentState.SetUInt256($"Balance:{to}", initialToBalance);
            SetupMessage(Pair, from);
            
            pair
                .Invoking(p => p.TransferTo(to, amount))
                .Should().Throw<OverflowException>();
        }
        
        [Fact]
        public void TransferTo_Throws_ToBalanceOverflow()
        {
            var pair = CreateNewOpdexPair();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 1;
            UInt256 initialFromBalance = 100;
            var initialToBalance = UInt256.MaxValue;
         
            PersistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            PersistentState.SetUInt256($"Balance:{to}", initialToBalance);
            SetupMessage(Pair, from);
            
            pair
                .Invoking(p => p.TransferTo(to, amount))
                .Should().Throw<OverflowException>();
        }
        
        [Fact]
        public void TransferFrom_Success()
        {
            var pair = CreateNewOpdexPair();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 30;
            UInt256 initialFromBalance = 200;
            UInt256 initialToBalance = 50;
            UInt256 initialSpenderAllowance = 100;
            UInt256 finalFromBalance = 170;
            UInt256 finalToBalance = 80;
            UInt256 finalSpenderAllowance = 70;
            var expectedTransferEvent = new TransferEvent {From = from, To = to, Amount = amount, EventTypeId = (byte)EventType.TransferEvent};
         
            PersistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            PersistentState.SetUInt256($"Balance:{to}", initialToBalance);
            PersistentState.SetUInt256($"Allowance:{from}:{to}", initialSpenderAllowance);
            SetupMessage(Pair, to);

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
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 150;
            UInt256 initialFromBalance = 100;
            UInt256 spenderAllowance = 150;
         
            PersistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            PersistentState.SetUInt256($"Allowance:{from}:{to}", spenderAllowance);
            SetupMessage(Pair, to);
            
            pair
                .Invoking(p => p.TransferFrom(from, to, amount))
                .Should().Throw<OverflowException>();
        }
        
        [Fact]
        public void TransferFrom_Throws_InsufficientSpenderAllowance()
        {
            var pair = CreateNewOpdexPair();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 200;
            UInt256 initialFromBalance = 1000;
            UInt256 spenderAllowance = 150;
         
            PersistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            PersistentState.SetUInt256($"Allowance:{from}:{to}", spenderAllowance);
            SetupMessage(Pair, to);
            
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
            var from = Trader0;
            var spender = Trader1;
            UInt256 amount = 100;
            var expectedApprovalEvent = new ApprovalEvent {Owner = from, Spender = spender, Amount = amount, EventTypeId = (byte)EventType.ApprovalEvent};
            
            SetupMessage(Pair, from);

            if (isIStandardContractImplementation)
            {
                var currentAmount = UInt256.MaxValue; // doesn't matter, unused
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
            const ulong currentBalanceCrs = 100_000_000;
            UInt256 currentBalanceToken = 1_900_000_000;
            UInt256 currentReserveCrs = 0;
            UInt256 currentReserveSrc = 0;
            UInt256 currentTotalSupply = 0;
            UInt256 currentKLast = 0;
            UInt256 currentFeeToBalance = 0;
            UInt256 currentTraderBalance = 0;
            UInt256 expectedLiquidity = 435888894;

            var pair = CreateNewOpdexPair(currentBalanceCrs);
            
            PersistentState.SetUInt64("ReserveCrs", (ulong)currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            PersistentState.SetUInt256("TotalSupply", currentTotalSupply);
            PersistentState.SetUInt256("KLast", currentKLast);
            PersistentState.SetUInt256($"Balance:{FeeTo}", currentFeeToBalance);
            PersistentState.SetUInt256($"Balance:{Trader0}", currentTraderBalance);
            
            var expectedSrcBalanceParams = new object[] {Pair};
            SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));
            SetupCall(Controller, 0ul, "GetFeeTo", null, TransferResult.Transferred(FeeTo));

            var mintedLiquidity = pair.Mint(Trader0);

            mintedLiquidity.Should().Be(expectedLiquidity);
        }
        
        [Fact]
        // Todo: Finish this
        public void MintWithExistingReserves_Success()
        {
            const ulong currentBalanceCrs = 5_500ul;
            UInt256 currentBalanceToken = 11_000;
            UInt256 currentReserveCrs = 5_000;
            UInt256 currentReserveSrc = 10_000;
            UInt256 currentTotalSupply = 2500;
            UInt256 expectedLiquidity = 250;
            UInt256 expectedKLast = 50_000_000;
            UInt256 currentFeeToBalance = 100;
            UInt256 currentTraderBalance = 0;
            UInt256 mintedFee = 0; // Todo: Calculate and set

            var pair = CreateNewOpdexPair(currentBalanceCrs);
            
            PersistentState.SetUInt64("ReserveCrs", (ulong)currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            PersistentState.SetUInt256("TotalSupply", currentTotalSupply);
            PersistentState.SetUInt256("KLast", expectedKLast);
            PersistentState.SetUInt256($"Balance:{FeeTo}", currentFeeToBalance);
            PersistentState.SetUInt256($"Balance:{Trader0}", currentTraderBalance);

            var expectedSrcBalanceParams = new object[] {Pair};
            SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));
            SetupCall(Controller, 0ul, "get_FeeTo", null, TransferResult.Transferred(FeeTo));
            
            var mintedLiquidity = pair.Mint(Trader0);

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
            UInt256 swapAmountCrs = 500;
            UInt256 currentReserveCrs = 5_500;
            UInt256 currentReserveSrc = 10_000;
            UInt256 expectedReceivedToken = 997;
            
            var pair = CreateNewOpdexPair((ulong)currentReserveCrs);
            
            PersistentState.SetUInt64("ReserveCrs", (ulong)currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
        }
        
        #endregion
        
        #region Borrow Tests

        [Fact]
        public void BorrowCrs_Success()
        {
            UInt256 currentBalanceToken = 1000;
            var callbackAddress = OtherAddress;
            const string  callbackMethod = "SomeMethod";
            const ulong expectedBalanceCrs = 1000;
            const ulong borrowedCrs = 100;
            
            var pair = CreateNewOpdexPair(expectedBalanceCrs);
            
            PersistentState.SetUInt256("ReserveCrs", expectedBalanceCrs);
            
            var expectedSrcBalanceParams = new object[] {Pair};
            SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));
            
            // Call callback method
            var bytes = Serializer.Serialize(new object[] {"some", true, borrowedCrs});
            var expectedBytesParams = new object[] {bytes};
            SetupCall(callbackAddress, borrowedCrs, callbackMethod, expectedBytesParams, TransferResult.Empty(), ReturnDebtCallback);

            pair.Borrow(borrowedCrs, 0ul, callbackAddress, callbackMethod, bytes);

            VerifyCall(OtherAddress, borrowedCrs, callbackMethod, expectedBytesParams, Times.Once);

            pair.Balance.Should().Be(expectedBalanceCrs);

            // Moq testing callback that simulates the actual, in contract,
            // callback to another contract with borrowed funds that would
            // return borrowed tokens.
            void ReturnDebtCallback()
            {
                SetupBalance(expectedBalanceCrs);
            }
        }

        [Fact]
        public void BorrowSrc_Success()
        {
            UInt256 currentBalanceToken = 1000;
            var callbackAddress = OtherAddress;
            const string callbackMethod = "SomeMethod";
            const ulong expectedBalanceCrs = 1000;
            const ulong borrowedCrs = 0;
            UInt256 expectedReserveSrc = 1000;
            UInt256 borrowedSrc = 100;
            
            var pair = CreateNewOpdexPair(expectedBalanceCrs);
            
            PersistentState.SetUInt256("ReserveCrs", expectedBalanceCrs);
            PersistentState.SetUInt256("ReserveSrc", expectedReserveSrc);
            
            var expectedSrcBalanceParams = new object[] {Pair};
            SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));

            // Send SRC to callback address prior to callback
            var expectedSrcTransferParams = new object[] {callbackAddress, borrowedSrc};
            SetupCall(Token, 0, "TransferTo", expectedSrcTransferParams, TransferResult.Transferred(true));
            
            // Call callback method
            var bytes = Serializer.Serialize(new object[] {"some", true, borrowedSrc});
            var expectedBytesParams = new object[] {bytes};
            SetupCall(callbackAddress, borrowedCrs, callbackMethod, expectedBytesParams, TransferResult.Empty(), ReturnedDebtCallback);

            pair.Borrow(0ul, borrowedSrc, callbackAddress, callbackMethod, bytes);
            
            pair.Balance.Should().Be(expectedBalanceCrs);

            VerifyCall(OtherAddress, borrowedCrs, callbackMethod, expectedBytesParams, Times.Once);
            VerifyCall(Token, 0, "TransferTo", expectedSrcTransferParams, Times.Once);
            VerifyCall(Token, 0, "GetBalance", expectedSrcBalanceParams, Times.Exactly(2));

            // Moq testing callback that simulates the actual, in contract,
            // callback to another contract with borrowed funds that would
            // return borrowed tokens.
            void ReturnedDebtCallback()
            {
                SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));
            }
        }

        [Fact]
        public void BorrowCrsAndSrc_Success()
        {
            
        }
        
        [Fact]
        public void BorrowCrsAndSrc_Throws_CrsDebtUnpaid()
        {
            
        }
        
        [Fact]
        public void BorrowCrsAndSrc_Throws_SrcDebtUnpaid()
        {
            
        }

        [Fact]
        public void BorrowCrs_Throws_UnpaidDebt()
        {
            UInt256 currentBalanceToken = 1000;
            var callbackAddress = OtherAddress;
            const string  callbackMethod = "SomeMethod";
            const ulong expectedBalanceCrs = 1000;
            const ulong borrowedCrs = 100;
            
            var pair = CreateNewOpdexPair(expectedBalanceCrs);
            
            PersistentState.SetUInt256("ReserveCrs", expectedBalanceCrs);
            
            var expectedSrcBalanceParams = new object[] {Pair};
            SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));
            
            var expectedParameters = new object[] {"some", true, borrowedCrs};
            var bytes = Serializer.Serialize(expectedParameters);
            SetupCall(callbackAddress, borrowedCrs, callbackMethod, new object[] {bytes}, TransferResult.Empty());
            
            pair
                .Invoking(p => p.Borrow(borrowedCrs, 0ul, callbackAddress, callbackMethod, bytes))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_DEBT_PAID");
        }
        
        [Fact]
        public void BorrowSrc_Throws_UnpaidDebt()
        {
            UInt256 currentBalanceToken = 1000;
            var callbackAddress = OtherAddress;
            const string callbackMethod = "SomeMethod";
            const ulong expectedBalanceCrs = 1000;
            const ulong borrowedCrs = 0;
            UInt256 expectedReserveSrc = 1000;
            UInt256 borrowedSrc = 100;
            
            var pair = CreateNewOpdexPair(expectedBalanceCrs);
            
            PersistentState.SetUInt256("ReserveCrs", expectedBalanceCrs);
            PersistentState.SetUInt256("ReserveSrc", expectedReserveSrc);
            
            var expectedSrcBalanceParams = new object[] {Pair};
            SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));

            // Send SRC to callback address prior to callback
            var expectedSrcTransferParams = new object[] {callbackAddress, borrowedSrc};
            SetupCall(Token, 0, "TransferTo", expectedSrcTransferParams, TransferResult.Transferred(true), DeductDebtCallback);
            
            // Call callback method
            var bytes = Serializer.Serialize(new object[] {"some", true, borrowedSrc});
            var expectedBytesParams = new object[] {bytes};
            SetupCall(callbackAddress, borrowedCrs, callbackMethod, expectedBytesParams, TransferResult.Empty());
            
            pair
                .Invoking(p => p.Borrow(0ul, borrowedSrc, callbackAddress, callbackMethod, bytes))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_DEBT_PAID");

            // Moq testing callback that simulates the actual, in contract,
            // callback to another contract with borrowed funds that would
            // return, or not return, the borrowed tokens.
            void DeductDebtCallback()
            {
                SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken - borrowedSrc));
            }
        }
        
        #endregion
    }
}
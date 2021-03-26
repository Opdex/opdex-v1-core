using System;
using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexCoreContracts.Tests
{
    public class OpdexStandardPoolTests : BaseContractTest
    {
        [Fact]
        public void CreatesNewPool_Success()
        {
            var pool = CreateNewOpdexStandardPool();

            pool.Token.Should().Be(Token);
            pool.Decimals.Should().Be(8);
            pool.Name.Should().Be("Opdex Liquidity Pool Token");
            pool.Symbol.Should().Be("OLPT");
        }

        [Fact]
        public void GetBalance_Success()
        {
            UInt256 expected = 100;
            PersistentState.SetUInt256($"Balance:{Trader0}", expected);

            var pool = CreateNewOpdexStandardPool();
            
            pool.GetBalance(Trader0).Should().Be(expected);
        }

        [Fact]
        public void GetAllowance_Success()
        {
            UInt256 expected = 100;
            PersistentState.SetUInt256($"Allowance:{Trader0}:{Trader1}", expected);

            var pool = CreateNewOpdexStandardPool();
            
            pool.GetAllowance(Trader0, Trader1).Should().Be(expected);
        }

        [Fact]
        public void GetReserves_Success()
        {
            ulong expectedCrs = 100;
            UInt256 expectedToken = 150;

            PersistentState.SetUInt64("ReserveCrs", expectedCrs);
            PersistentState.SetUInt256("ReserveSrc", expectedToken);
            
            var pool = CreateNewOpdexStandardPool();

            var reserves = pool.Reserves;
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

            var pool = CreateNewOpdexStandardPool(expectedBalanceCrs);
            
            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceToken));

            pool.Sync();

            pool.ReserveCrs.Should().Be(expectedBalanceCrs);
            pool.ReserveSrc.Should().Be(expectedBalanceToken);

            VerifyCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, Times.Once);
            VerifyLog(new OpdexSyncEvent
            {
                ReserveCrs = expectedBalanceCrs, 
                ReserveSrc = expectedBalanceToken
            }, Times.Once);
        }

        [Fact]
        public void Skim_Success()
        {
            const ulong expectedBalanceCrs = 100;
            UInt256 expectedBalanceToken = 150;
            const ulong currentReserveCrs = 50;
            UInt256 currentReserveSrc = 100;

            var pool = CreateNewOpdexStandardPool(expectedBalanceCrs);
            
            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceToken));

            var expectedTransferToParams = new object[] { Trader0, (UInt256)50 };
            SetupCall(Token, 0ul, "TransferTo", expectedTransferToParams, TransferResult.Transferred(true));

            SetupTransfer(Trader0, 50ul, TransferResult.Transferred(true));
            
            pool.Skim(Trader0);

            VerifyCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, Times.Once);
            VerifyCall( Token, 0ul, "TransferTo", expectedTransferToParams, Times.Once);
            VerifyTransfer(Trader0, 50ul, Times.Once);
        }
        
        #region Liquidity Pool Token Tests

        [Fact]
        public void TransferTo_Success()
        {
            var pool = CreateNewOpdexStandardPool();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 75;
            UInt256 initialFromBalance = 200;
            UInt256 initialToBalance = 25;
            UInt256 finalFromBalance = 125;
            UInt256 finalToBalance = 100;
         
            PersistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            PersistentState.SetUInt256($"Balance:{to}", initialToBalance);
            SetupMessage(Pool, from);

            var success = pool.TransferTo(to, amount);

            success.Should().BeTrue();
            pool.GetBalance(from).Should().Be(finalFromBalance);
            pool.GetBalance(to).Should().Be(finalToBalance);
            
            VerifyLog(new OpdexTransferEvent
            {
                From = from, 
                To = to, 
                Amount = amount
            }, Times.Once);
        }

        [Fact]
        public void TransferTo_Throws_InsufficientFromBalance()
        {
            var pool = CreateNewOpdexStandardPool();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 115;
            UInt256 initialFromBalance = 100;
            UInt256 initialToBalance = 0;
         
            PersistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            PersistentState.SetUInt256($"Balance:{to}", initialToBalance);
            SetupMessage(Pool, from);
            
            pool
                .Invoking(p => p.TransferTo(to, amount))
                .Should().Throw<OverflowException>();
        }
        
        [Fact]
        public void TransferTo_Throws_ToBalanceOverflow()
        {
            var pool = CreateNewOpdexStandardPool();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 1;
            UInt256 initialFromBalance = 100;
            var initialToBalance = UInt256.MaxValue;
         
            PersistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            PersistentState.SetUInt256($"Balance:{to}", initialToBalance);
            SetupMessage(Pool, from);
            
            pool
                .Invoking(p => p.TransferTo(to, amount))
                .Should().Throw<OverflowException>();
        }
        
        [Fact]
        public void TransferFrom_Success()
        {
            var pool = CreateNewOpdexStandardPool();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 30;
            UInt256 initialFromBalance = 200;
            UInt256 initialToBalance = 50;
            UInt256 initialSpenderAllowance = 100;
            UInt256 finalFromBalance = 170;
            UInt256 finalToBalance = 80;
            UInt256 finalSpenderAllowance = 70;
         
            PersistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            PersistentState.SetUInt256($"Balance:{to}", initialToBalance);
            PersistentState.SetUInt256($"Allowance:{from}:{to}", initialSpenderAllowance);
            SetupMessage(Pool, to);

            pool.TransferFrom(from, to, amount).Should().BeTrue();
            pool.GetBalance(from).Should().Be(finalFromBalance);
            pool.GetBalance(to).Should().Be(finalToBalance);
            pool.GetAllowance(from, to).Should().Be(finalSpenderAllowance);
            
            VerifyLog(new OpdexTransferEvent
            {
                From = from, 
                To = to, 
                Amount = amount
            }, Times.Once);
        }

        [Fact]
        public void TransferFrom_Throws_InsufficientFromBalance()
        {
            var pool = CreateNewOpdexStandardPool();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 150;
            UInt256 initialFromBalance = 100;
            UInt256 spenderAllowance = 150;
         
            PersistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            PersistentState.SetUInt256($"Allowance:{from}:{to}", spenderAllowance);
            SetupMessage(Pool, to);
            
            pool
                .Invoking(p => p.TransferFrom(from, to, amount))
                .Should().Throw<OverflowException>();
        }
        
        [Fact]
        public void TransferFrom_Throws_InsufficientSpenderAllowance()
        {
            var pool = CreateNewOpdexStandardPool();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 200;
            UInt256 initialFromBalance = 1000;
            UInt256 spenderAllowance = 150;
         
            PersistentState.SetUInt256($"Balance:{from}", initialFromBalance);
            PersistentState.SetUInt256($"Allowance:{from}:{to}", spenderAllowance);
            SetupMessage(Pool, to);
            
            pool
                .Invoking(p => p.TransferFrom(from, to, amount))
                .Should().Throw<OverflowException>();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Approve_Success(bool isIStandardContractImplementation)
        {
            var pool = CreateNewOpdexStandardPool();
            var from = Trader0;
            var spender = Trader1;
            UInt256 amount = 100;
            
            SetupMessage(Pool, from);

            if (isIStandardContractImplementation)
            {
                var currentAmount = UInt256.MaxValue; // doesn't matter, unused
                pool.Approve(spender, currentAmount, amount).Should().BeTrue();
            }
            else
            {
                pool.Approve(spender, amount).Should().BeTrue();
            }
            
            VerifyLog(new OpdexApprovalEvent
            {
                Owner = from, 
                Spender = spender, 
                Amount = amount
            }, Times.Once);
        }
        
        #endregion
        
        #region Mint Tests

        [Fact]
        public void MintInitialLiquidity_Success()
        {
            const ulong currentBalanceCrs = 100_000_000;
            UInt256 currentBalanceToken = 1_900_000_000;
            const ulong currentReserveCrs = 0;
            UInt256 currentReserveSrc = 0;
            UInt256 currentTotalSupply = 0;
            UInt256 currentKLast = 0;
            UInt256 currentFeeToBalance = 0;
            UInt256 currentTraderBalance = 0;
            UInt256 expectedLiquidity = 435888894;
            UInt256 expectedKLast = 190_000_000_000_000_000;
            UInt256 expectedBurnAmount = 1_000;
            var trader = Trader0;

            var pool = CreateNewOpdexStandardPool(currentBalanceCrs);
            
            SetupMessage(Pool, Trader0);
            
            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            PersistentState.SetUInt256("TotalSupply", currentTotalSupply);
            PersistentState.SetUInt256("KLast", currentKLast);
            PersistentState.SetUInt256($"Balance:{FeeTo}", currentFeeToBalance);
            PersistentState.SetUInt256($"Balance:{trader}", currentTraderBalance);
            
            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));

            var mintedLiquidity = pool.Mint(trader);
            mintedLiquidity.Should().Be(expectedLiquidity);
            
            pool.KLast.Should().Be(expectedKLast);
            pool.TotalSupply.Should().Be(expectedLiquidity + expectedBurnAmount); // burned
            pool.ReserveCrs.Should().Be(currentBalanceCrs);
            pool.ReserveSrc.Should().Be(currentBalanceToken);

            var traderBalance = pool.GetBalance(trader);
            traderBalance.Should().Be(expectedLiquidity);

            VerifyLog(new OpdexSyncEvent
            {
                ReserveCrs = currentBalanceCrs,
                ReserveSrc = currentBalanceToken
            }, Times.Once);
            
            VerifyLog(new OpdexMintEvent
            {
                AmountCrs = currentBalanceCrs,
                AmountSrc = currentBalanceToken,
                Sender = trader
            }, Times.Once);
            
            VerifyLog(new OpdexTransferEvent
            {
                From = Address.Zero,
                To = Address.Zero,
                Amount = expectedBurnAmount
            }, Times.Once);

            VerifyLog(new OpdexTransferEvent
            {
                From = Address.Zero,
                To = trader,
                Amount = expectedLiquidity
            }, Times.Once);
        }
        
        [Fact]
        public void MintWithExistingReserves_Success()
        {
            const ulong currentBalanceCrs = 5_500ul;
            UInt256 currentBalanceToken = 11_000;
            const ulong currentReserveCrs = 5_000;
            UInt256 currentReserveSrc = 10_000;
            UInt256 currentTotalSupply = 2500;
            UInt256 expectedLiquidity = 250;
            UInt256 expectedKLast = 45_000_000;
            UInt256 expectedK = currentBalanceCrs * currentBalanceToken;
            UInt256 currentFeeToBalance = 100;
            UInt256 currentTraderBalance = 0;
            var trader = Trader0;

            var pool = CreateNewOpdexStandardPool(currentBalanceCrs);
            SetupMessage(Pool, trader);
            
            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            PersistentState.SetUInt256("TotalSupply", currentTotalSupply);
            PersistentState.SetUInt256("KLast", expectedKLast);
            PersistentState.SetUInt256($"Balance:{FeeTo}", currentFeeToBalance);
            PersistentState.SetUInt256($"Balance:{Trader0}", currentTraderBalance);

            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));
            SetupCall(Controller, 0ul, "get_FeeTo", null, TransferResult.Transferred(FeeTo));
            
            var mintedLiquidity = pool.Mint(Trader0);
            mintedLiquidity.Should().Be(expectedLiquidity);
            
            pool.KLast.Should().Be(expectedK);
            pool.TotalSupply.Should().Be(currentTotalSupply + expectedLiquidity);
            pool.ReserveCrs.Should().Be(currentBalanceCrs);
            pool.ReserveSrc.Should().Be(currentBalanceToken);

            var traderBalance = pool.GetBalance(trader);
            traderBalance.Should().Be(expectedLiquidity);
            
            VerifyLog(new OpdexSyncEvent
            {
                ReserveCrs = currentBalanceCrs,
                ReserveSrc = currentBalanceToken
            }, Times.Once);
            
            VerifyLog(new OpdexMintEvent
            {
                AmountCrs = 500,
                AmountSrc = 1000,
                Sender = trader
            }, Times.Once);

            VerifyLog(new OpdexTransferEvent
            {
                From = Address.Zero,
                To = trader,
                Amount = expectedLiquidity
            }, Times.Once);
        }

        [Fact]
        public void Mint_Throws_InsufficientLiquidity()
        {
            const ulong currentBalanceCrs = 1000;
            UInt256 currentBalanceToken = 1000;
            const ulong currentReserveCrs = 0;
            UInt256 currentReserveSrc = 0;
            UInt256 currentTotalSupply = 0;
            UInt256 currentKLast = 0;
            UInt256 currentFeeToBalance = 0;
            UInt256 currentTraderBalance = 0;
            var trader = Trader0;

            var pool = CreateNewOpdexStandardPool(currentBalanceCrs);
            
            SetupMessage(Pool, Trader0);
            
            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            PersistentState.SetUInt256("TotalSupply", currentTotalSupply);
            PersistentState.SetUInt256("KLast", currentKLast);
            PersistentState.SetUInt256($"Balance:{FeeTo}", currentFeeToBalance);
            PersistentState.SetUInt256($"Balance:{trader}", currentTraderBalance);
            
            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, "GetBalance", expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));
            
            pool
                .Invoking(p => p.Mint(trader))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }

        [Fact]
        public void Mint_Throws_ContractLocked()
        {
            var pool = CreateNewOpdexStandardPool();

            SetupMessage(Pool, Controller);
            
            PersistentState.SetBool("Locked", true);

            pool
                .Invoking(p => p.Mint(Trader0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }
        
        #endregion
        
        #region Burn Tests
        
        [Fact]
        public void BurnPartialLiquidity_Success()
        {
            const ulong currentReserveCrs = 100_000;
            UInt256 currentReserveSrc = 1_000_000;
            UInt256 currentTotalSupply = 15_000;
            UInt256 currentKLast = 90_000_000_000;
            UInt256 burnAmount = 1_200;
            const ulong expectedReceivedCrs = 8_000;
            UInt256 expectedReceivedSrc = 80_000;
            var to = Trader0;

            var pool = CreateNewOpdexStandardPool(currentReserveCrs);
            SetupMessage(Pool, Controller);
            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            PersistentState.SetUInt256("TotalSupply", currentTotalSupply);
            PersistentState.SetUInt256($"Balance:{Pool}", burnAmount);
            PersistentState.SetUInt256("KLast", currentKLast);
            
            SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc));
            SetupCall(Controller, 0ul, "get_FeeTo", null, TransferResult.Transferred(FeeTo));
            SetupTransfer(to, expectedReceivedCrs, TransferResult.Transferred(true));
            SetupCall(Token, 0, "TransferTo", new object[] { to, expectedReceivedSrc }, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - expectedReceivedSrc));
            });

            var results = pool.Burn(to);
            results[0].Should().Be((UInt256)expectedReceivedCrs);
            results[1].Should().Be(expectedReceivedSrc);
            pool.KLast.Should().Be((currentReserveCrs - expectedReceivedCrs) * (currentReserveSrc - expectedReceivedSrc));
            pool.Balance.Should().Be(currentReserveCrs - expectedReceivedCrs);
            pool.TotalSupply.Should().Be(currentTotalSupply  - burnAmount);

            // Burn Tokens
            VerifyLog(new OpdexTransferEvent
            {
                From = Pool,
                To = Address.Zero,
                Amount = burnAmount
            }, Times.Once);
            
            // Burn Event Summary
            VerifyLog(new OpdexBurnEvent
            {
                Sender = Controller,
                To = to,
                AmountCrs = expectedReceivedCrs,
                AmountSrc = expectedReceivedSrc
            }, Times.Once);
        }
        
        [Fact]
        public void BurnAllLiquidity_Success()
        {
            const ulong currentReserveCrs = 100_000;
            UInt256 currentReserveSrc = 1_000_000;
            UInt256 currentTotalSupply = 15_000;
            UInt256 currentKLast = 100_000_000_000;
            UInt256 burnAmount = 14_000; // Total Supply - Minimum Liquidity
            const ulong expectedReceivedCrs = 93_333;
            UInt256 expectedReceivedSrc = 933_333;
            UInt256 expectedMintedFee = 0;
            var to = Trader0;

            var pool = CreateNewOpdexStandardPool(currentReserveCrs);
            SetupMessage(Pool, Controller);
            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            PersistentState.SetUInt256("TotalSupply", currentTotalSupply);
            PersistentState.SetUInt256($"Balance:{Pool}", burnAmount);
            PersistentState.SetUInt256("KLast", currentKLast);
            
            SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc));
            SetupCall(Controller, 0ul, "get_FeeTo", new object[0], TransferResult.Transferred(FeeTo));
            SetupTransfer(to, expectedReceivedCrs, TransferResult.Transferred(true));
            SetupCall(Token, 0, "TransferTo", new object[] { to, expectedReceivedSrc }, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - expectedReceivedSrc));
            });

            var results = pool.Burn(to);
            results[0].Should().Be((UInt256)expectedReceivedCrs);
            results[1].Should().Be(expectedReceivedSrc);
            pool.KLast.Should().Be((currentReserveCrs - expectedReceivedCrs) * (currentReserveSrc - expectedReceivedSrc));
            pool.Balance.Should().Be(currentReserveCrs - expectedReceivedCrs);
            pool.TotalSupply.Should().Be(currentTotalSupply + expectedMintedFee - burnAmount);

            // Burn Tokens
            VerifyLog(new OpdexTransferEvent
            {
                From = Pool,
                To = Address.Zero,
                Amount = burnAmount
            }, Times.Once);
            
            // Burn Event Summary
            VerifyLog(new OpdexBurnEvent
            {
                Sender = Controller,
                To = to,
                AmountCrs = expectedReceivedCrs,
                AmountSrc = expectedReceivedSrc
            }, Times.Once);
        }

        [Fact]
        public void Burn_Throws_InsufficientLiquidityBurned()
        {
            const ulong currentReserveCrs = 100_000;
            UInt256 currentReserveSrc = 1_000_000;
            UInt256 currentTotalSupply = 15_000;
            UInt256 currentKLast = 90_000_000_000;
            UInt256 burnAmount = 0;
            var to = Trader0;

            var pool = CreateNewOpdexStandardPool(currentReserveCrs);
            SetupMessage(Pool, Controller);
            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            PersistentState.SetUInt256("TotalSupply", currentTotalSupply);
            PersistentState.SetUInt256($"Balance:{Pool}", burnAmount);
            PersistentState.SetUInt256("KLast", currentKLast);
            
            SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc));

            pool
                .Invoking(p => p.Burn(to))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY_BURNED");
        }

        [Fact]
        public void Burn_Throws_LockedContract()
        {
            var pool = CreateNewOpdexStandardPool();

            SetupMessage(Pool, Controller);
            
            PersistentState.SetBool("Locked", true);

            pool
                .Invoking(p => p.Burn(Trader0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }
        
        #endregion
        
        #region Swap Tests

        [Fact]
        public void SwapCrsForSrc_Success()
        {
            const ulong swapAmountCrs = 17_000;
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            UInt256 expectedReceivedToken = 7_259;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller, swapAmountCrs);

            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            SetupCall(Token, 0, "TransferTo", new object[] { to, expectedReceivedToken }, TransferResult.Transferred(true));
            SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - expectedReceivedToken));

            pool.Swap(0, expectedReceivedToken, to, new byte[0]);

            pool.ReserveCrs.Should().Be(currentReserveCrs + swapAmountCrs);
            pool.ReserveSrc.Should().Be(currentReserveSrc - expectedReceivedToken);
            pool.Balance.Should().Be(467_000);
            
            VerifyCall(Token, 0, "TransferTo", new object[] {to, expectedReceivedToken}, Times.Once);
            VerifyCall(Token, 0, "GetBalance", new object[] {Pool}, Times.Once);
            
            VerifyLog(new OpdexSyncEvent
            {
                ReserveCrs = currentReserveCrs + swapAmountCrs,
                ReserveSrc = (currentReserveSrc - expectedReceivedToken)
            }, Times.Once);

            VerifyLog(new OpdexSwapEvent
            {
                AmountCrsIn = swapAmountCrs,
                AmountCrsOut = 0,
                AmountSrcIn = 0,
                AmountSrcOut = expectedReceivedToken,
                Sender = Controller,
                To = to
            }, Times.Once);
        }

        [Fact]
        public void SwapSrcForCrs_Success()
        {
            UInt256 swapAmountSrc = 2_941;
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            const ulong expectedCrsReceived = 6_500;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);
            
            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            
            SetupTransfer(to, expectedCrsReceived, TransferResult.Transferred(true));
            SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + swapAmountSrc));

            pool.Swap(expectedCrsReceived, 0, to, new byte[0]);
            
            pool.ReserveCrs.Should().Be(currentReserveCrs - expectedCrsReceived);
            pool.ReserveSrc.Should().Be(currentReserveSrc + swapAmountSrc);
            pool.Balance.Should().Be(443_500);

            VerifyLog(new OpdexSyncEvent
            {
                ReserveCrs = currentReserveCrs - expectedCrsReceived,
                ReserveSrc = (currentReserveSrc + swapAmountSrc)
            }, Times.Once);

            VerifyLog(new OpdexSwapEvent
            {
                AmountCrsIn = 0,
                AmountCrsOut = expectedCrsReceived,
                AmountSrcIn = swapAmountSrc,
                AmountSrcOut = 0,
                Sender = Controller,
                To = to
            }, Times.Once);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        public void Swap_Throws_InvalidOutputAmount(ulong amountCrsOut, UInt256 amountSrcOut)
        {
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);
            SetupMessage(Pool, Controller);
            
            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            
            pool
                .Invoking(p => p.Swap(amountCrsOut, amountSrcOut, Trader0, new byte[0]))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_OUTPUT_AMOUNT");
        }
        
        [Theory]
        [InlineData(450_001, 0)]
        [InlineData(0, 200_001)]
        public void Swap_Throws_InsufficientLiquidity(ulong amountCrsOut, UInt256 amountSrcOut)
        {
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);
            SetupMessage(Pool, Controller);
            
            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            
            pool
                .Invoking(p => p.Swap(amountCrsOut, amountSrcOut, Trader0, new byte[0]))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }
        
        [Fact]
        public void Swap_Throws_InvalidTo()
        {
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            var addresses = new[] {Pool, Token};
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);
            SetupMessage(Pool, Controller);
            
            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            
            foreach(var address in addresses)
            {
                PersistentState.SetBool("Locked", false);
                
                pool
                    .Invoking(p => p.Swap(1000, 0, address, new byte[0]))
                    .Should().Throw<SmartContractAssertException>()
                    .WithMessage("OPDEX: INVALID_TO");
            }
        }

        [Fact]
        public void Swap_Throws_ZeroCrsInputAmount()
        {
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            UInt256 expectedReceivedToken = 7_259;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);

            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            SetupCall(Token, 0, "TransferTo", new object[] { to, expectedReceivedToken }, TransferResult.Transferred(true));
            SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - expectedReceivedToken));

            pool
                .Invoking(p => p.Swap(0, expectedReceivedToken, to, new byte[0]))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: ZERO_INPUT_AMOUNT");
        }

        [Fact]
        public void Swap_Throws_ZeroSrcInputAmount()
        {
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            const ulong expectedCrsReceived = 6_500;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);
            
            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            
            SetupTransfer(to, expectedCrsReceived, TransferResult.Transferred(true));
            SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc));

            pool
                .Invoking(p => p.Swap(expectedCrsReceived, 0, to, new byte[0]))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: ZERO_INPUT_AMOUNT");
        }
        
        [Fact]
        public void Swap_Throws_InsufficientCrsInputAmount()
        {
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            UInt256 expectedReceivedToken = 7_259;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller, 1);

            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            SetupCall(Token, 0, "TransferTo", new object[] { to, expectedReceivedToken }, TransferResult.Transferred(true));
            SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - expectedReceivedToken));

            pool
                .Invoking(p => p.Swap(0, expectedReceivedToken, to, new byte[0]))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        }

        [Fact]
        public void Swap_Throws_InsufficientSrcInputAmount()
        {
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            const ulong expectedCrsReceived = 6_500;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);
            
            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);
            
            SetupTransfer(to, expectedCrsReceived, TransferResult.Transferred(true));
            SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + 1));

            pool
                .Invoking(p => p.Swap(expectedCrsReceived, 0, to, new byte[0]))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        }

        [Fact]
        public void Swap_Throws_LockedContract()
        {
            var pool = CreateNewOpdexStandardPool();

            SetupMessage(Pool, Controller);
            
            PersistentState.SetBool("Locked", true);

            pool
                .Invoking(p => p.Swap(0, 10, Trader0, new byte[0]))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }
        
        #endregion
        
        #region FlashSwap

        [Fact]
        public void Swap_BorrowSrcReturnSrc_Success()
        {
            UInt256 borrowedSrc = 17_000;
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            UInt256 expectedFee = 52;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);

            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            SetupCall(Token, 0, "TransferTo", new object[] { to, borrowedSrc }, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + expectedFee));
            });
            
            pool.Swap(0, borrowedSrc, to, Serializer.Serialize(callbackData));

            pool.ReserveCrs.Should().Be(currentReserveCrs);
            pool.ReserveSrc.Should().Be(currentReserveSrc + expectedFee);
            pool.Balance.Should().Be(currentReserveCrs);
            
            VerifyCall(Token, 0, "TransferTo", new object[] {to, borrowedSrc}, Times.Once);
            VerifyCall(Token, 0, "GetBalance", new object[] {Pool}, Times.Once);
            VerifyCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, Times.Once);
            
            VerifyLog(new OpdexSyncEvent
            {
                ReserveCrs = currentReserveCrs,
                ReserveSrc = (currentReserveSrc + expectedFee)
            }, Times.Once);

            VerifyLog(new OpdexSwapEvent
            {
                AmountCrsIn = 0,
                AmountCrsOut = 0,
                AmountSrcIn = 17_052,
                AmountSrcOut = 17_000,
                Sender = Controller,
                To = to
            }, Times.Once);
        }

        [Fact]
        public void Swap_BorrowSrcReturnCrs_Success()
        {
            UInt256 borrowedSrc = 2_941;
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            const ulong expectedCrsReceived = 6_737;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);

            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            SetupCall(Token, 0, "TransferTo", new object[] { to, borrowedSrc }, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - borrowedSrc));
                SetupBalance(currentReserveCrs + expectedCrsReceived);
            });
            
            pool.Swap(0, borrowedSrc, to, Serializer.Serialize(callbackData));

            pool.ReserveCrs.Should().Be(currentReserveCrs + expectedCrsReceived);
            pool.ReserveSrc.Should().Be(currentReserveSrc - borrowedSrc);
            pool.Balance.Should().Be(currentReserveCrs + expectedCrsReceived);
            
            VerifyCall(Token, 0, "TransferTo", new object[] {to, borrowedSrc}, Times.Once);
            VerifyCall(Token, 0, "GetBalance", new object[] {Pool}, Times.Once);
            VerifyCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, Times.Once);
            
            VerifyLog(new OpdexSyncEvent
            {
                ReserveCrs = currentReserveCrs + expectedCrsReceived,
                ReserveSrc = (currentReserveSrc - borrowedSrc)
            }, Times.Once);

            VerifyLog(new OpdexSwapEvent
            {
                AmountCrsIn = expectedCrsReceived,
                AmountCrsOut = 0,
                AmountSrcIn = 0,
                AmountSrcOut = borrowedSrc,
                Sender = Controller,
                To = to
            }, Times.Once);
        }

        [Fact]
        public void Swap_BorrowCrsReturnCrs_Success()
        {
            const ulong borrowedCrs = 4_500;
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            const ulong expectedCrsFee = 14;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);

            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            SetupTransfer(to, borrowedCrs, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc));
                SetupBalance(currentReserveCrs + expectedCrsFee);
            });
            
            pool.Swap(borrowedCrs, 0, to, Serializer.Serialize(callbackData));

            pool.ReserveCrs.Should().Be(currentReserveCrs + expectedCrsFee);
            pool.ReserveSrc.Should().Be(currentReserveSrc);
            pool.Balance.Should().Be(currentReserveCrs + expectedCrsFee);
            
            VerifyTransfer(to, borrowedCrs, Times.Once);
            VerifyCall(Token, 0, "GetBalance", new object[] {Pool}, Times.Once);
            VerifyCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, Times.Once);
            
            VerifyLog(new OpdexSyncEvent
            {
                ReserveCrs = currentReserveCrs + expectedCrsFee,
                ReserveSrc = currentReserveSrc
            }, Times.Once);

            VerifyLog(new OpdexSwapEvent
            {
                AmountCrsIn = borrowedCrs + expectedCrsFee,
                AmountCrsOut = borrowedCrs,
                AmountSrcIn = 0,
                AmountSrcOut = 0,
                Sender = Controller,
                To = to
            }, Times.Once);
        }

        [Fact]
        public void Swap_BorrowCrsReturnSrc_Success()
        {
            const ulong borrowedCrs = 4_500;
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            UInt256 expectedSrcReceived = 2_027;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);

            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            SetupTransfer(to, borrowedCrs, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + expectedSrcReceived));
            });
            
            pool.Swap(borrowedCrs, 0, to, Serializer.Serialize(callbackData));

            pool.ReserveCrs.Should().Be(currentReserveCrs - borrowedCrs);
            pool.ReserveSrc.Should().Be(currentReserveSrc + expectedSrcReceived);
            pool.Balance.Should().Be(currentReserveCrs - borrowedCrs);
            
            VerifyTransfer(to, borrowedCrs, Times.Once);
            VerifyCall(Token, 0, "GetBalance", new object[] {Pool}, Times.Once);
            VerifyCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, Times.Once);
            
            VerifyLog(new OpdexSyncEvent
            {
                ReserveCrs = currentReserveCrs - borrowedCrs,
                ReserveSrc = (currentReserveSrc + expectedSrcReceived)
            }, Times.Once);

            VerifyLog(new OpdexSwapEvent
            {
                AmountCrsIn = 0,
                AmountCrsOut = borrowedCrs,
                AmountSrcIn = expectedSrcReceived,
                AmountSrcOut = 0,
                Sender = Controller,
                To = to
            }, Times.Once);
        }
        
        [Fact]
        public void Swap_BorrowCrs_Throws_InsufficientInputAmount()
        {
            const ulong borrowedCrs = 4_500;
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            UInt256 expectedSrcReceived = 1;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);

            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            SetupTransfer(to, borrowedCrs, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + expectedSrcReceived));
            });
            
            pool
                .Invoking(p => p.Swap(borrowedCrs, 0 , to, Serializer.Serialize(callbackData)))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        }

        [Fact]
        public void Swap_BorrowSrc_Throws_InsufficientInputAmount()
        {
            UInt256 borrowedSrc = 2_941;
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            const ulong expectedCrsReceived = 1;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);

            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            SetupCall(Token, 0, "TransferTo", new object[] { to, borrowedSrc }, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - borrowedSrc));
                SetupBalance(currentReserveCrs + expectedCrsReceived);
            });
            
            pool
                .Invoking(p => p.Swap(0, borrowedSrc , to, Serializer.Serialize(callbackData)))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        }
        
        [Fact]
        public void Swap_BorrowCrs_Throws_ZeroInputAmount()
        {
            const ulong borrowedCrs = 4_500;
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            UInt256 expectedSrcReceived = 0;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);

            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            SetupTransfer(to, borrowedCrs, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method, new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + expectedSrcReceived));
            });
            
            pool
                .Invoking(p => p.Swap(borrowedCrs, 0 , to, Serializer.Serialize(callbackData)))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: ZERO_INPUT_AMOUNT");
        }

        [Fact]
        public void Swap_BorrowSrc_Throws_ZeroInputAmount()
        {
            UInt256 borrowedSrc = 2_941;
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            const ulong expectedCrsReceived = 0;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);

            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            SetupCall(Token, 0, "TransferTo", new object[] { to, borrowedSrc }, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - borrowedSrc));
                SetupBalance(currentReserveCrs + expectedCrsReceived);
            });
            
            pool
                .Invoking(p => p.Swap(0, borrowedSrc , to, Serializer.Serialize(callbackData)))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: ZERO_INPUT_AMOUNT");
        }
        
        [Fact]
        public void Swap_BorrowCrsReturnCrsAndSrc_Success()
        {
            const ulong borrowedCrs = 4_500;
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            UInt256 expectedSrcReceived = 1_012;
            const ulong expectedCrsReceived = 2_250;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);

            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            SetupTransfer(to, borrowedCrs, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + expectedSrcReceived));
                SetupBalance(currentReserveCrs - borrowedCrs + expectedCrsReceived);
            });
            
            pool.Swap(borrowedCrs, 0, to, Serializer.Serialize(callbackData));

            pool.ReserveCrs.Should().Be(currentReserveCrs - borrowedCrs + expectedCrsReceived);
            pool.ReserveSrc.Should().Be(currentReserveSrc + expectedSrcReceived);
            pool.Balance.Should().Be(currentReserveCrs - borrowedCrs + expectedCrsReceived);
            
            VerifyTransfer(to, borrowedCrs, Times.Once);
            VerifyCall(Token, 0, "GetBalance", new object[] {Pool}, Times.Once);
            VerifyCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, Times.Once);
            
            VerifyLog(new OpdexSyncEvent
            {
                ReserveCrs = currentReserveCrs - borrowedCrs + expectedCrsReceived,
                ReserveSrc = (currentReserveSrc + expectedSrcReceived)
            }, Times.Once);

            VerifyLog(new OpdexSwapEvent
            {
                AmountCrsIn = expectedCrsReceived,
                AmountCrsOut = borrowedCrs,
                AmountSrcIn = expectedSrcReceived,
                AmountSrcOut = 0,
                Sender = Controller,
                To = to
            }, Times.Once);
        }

        [Fact]
        public void Swap_BorrowSrcReturnCrsAndSrc_Success()
        {
            UInt256 borrowedSrc = 2_941;
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            const ulong expectedCrsReceived = 3_355;
            UInt256 expectedSrcReceived = 1_470;
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, Controller);

            PersistentState.SetUInt64("ReserveCrs", currentReserveCrs);
            PersistentState.SetUInt256("ReserveSrc", currentReserveSrc);

            SetupCall(Token, 0, "TransferTo", new object[] { to, borrowedSrc }, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, "GetBalance", new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - borrowedSrc + expectedSrcReceived));
                SetupBalance(currentReserveCrs + expectedCrsReceived);
            });
            
            pool.Swap(0, borrowedSrc, to, Serializer.Serialize(callbackData));

            pool.ReserveCrs.Should().Be(currentReserveCrs + expectedCrsReceived);
            pool.ReserveSrc.Should().Be(currentReserveSrc - borrowedSrc + expectedSrcReceived);
            pool.Balance.Should().Be(currentReserveCrs + expectedCrsReceived);
            
            VerifyCall(Token, 0, "TransferTo", new object[] {to, borrowedSrc}, Times.Once);
            VerifyCall(Token, 0, "GetBalance", new object[] {Pool}, Times.Once);
            VerifyCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, Times.Once);
            
            VerifyLog(new OpdexSyncEvent
            {
                ReserveCrs = currentReserveCrs + expectedCrsReceived,
                ReserveSrc = (currentReserveSrc - borrowedSrc + expectedSrcReceived)
            }, Times.Once);

            VerifyLog(new OpdexSwapEvent
            {
                AmountCrsIn = expectedCrsReceived,
                AmountCrsOut = 0,
                AmountSrcIn = expectedSrcReceived,
                AmountSrcOut = borrowedSrc,
                Sender = Controller,
                To = to
            }, Times.Once);
        }

        #endregion
    }
}
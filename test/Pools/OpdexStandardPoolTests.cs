using System;
using FluentAssertions;
using Microsoft.Extensions.ObjectPool;
using Moq;
using OpdexV1Core.Tests.Base;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Standards;
using Xunit;

namespace OpdexV1Core.Tests.Pools
{
    public class OpdexStandardPoolTests : TestBase
    {
        [Theory]
        [InlineData(true, false, 0, true)]
        [InlineData(true, true, 1, false)]
        [InlineData(true, false, 2, true)]
        [InlineData(true, false, 3, false)]
        [InlineData(false, false, 4, true)]
        [InlineData(true, true, 5, false)]
        [InlineData(true, false, 6, true)]
        [InlineData(false, false, 7, false)]
        [InlineData(true, false, 8, true)]
        [InlineData(false, true, 9, false)]
        [InlineData(true, true, 10, true)]
        public void CreatesNewPool_Success(bool authProviders, bool authTraders, uint fee, bool marketFeeEnabled)
        {
            var pool = CreateNewOpdexStandardPool(0ul, authProviders, authTraders, fee, marketFeeEnabled);
            
            pool.Token.Should().Be(Token);
            pool.Decimals.Should().Be(8);
            pool.Name.Should().Be("Opdex Liquidity Pool Token");
            pool.Symbol.Should().Be("OLPT");
            pool.AuthProviders.Should().Be(authProviders);
            pool.AuthTraders.Should().Be(authTraders);
            pool.TransactionFee.Should().Be(fee);
            pool.Market.Should().Be(StandardMarket);
            pool.MarketFeeEnabled.Should().Be(marketFeeEnabled);
        }
        
        [Fact]
        public void CreatesPool_Throws_InvalidToken()
        {
            var token = Address.Zero;
            var market = CreateNewOpdexStandardMarket();
            
            market
                .Invoking(c => c.CreatePool(token))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_TOKEN");
        }

        [Fact]
        public void GetBalance_Success()
        {
            UInt256 expected = 100;
            State.SetUInt256($"Balance:{Trader0}", expected);

            var pool = CreateNewOpdexStandardPool();
            
            pool.GetBalance(Trader0).Should().Be(expected);
        }

        [Fact]
        public void GetAllowance_Success()
        {
            UInt256 expected = 100;
            State.SetUInt256($"Allowance:{Trader0}:{Trader1}", expected);

            var pool = CreateNewOpdexStandardPool();
            
            pool.Allowance(Trader0, Trader1).Should().Be(expected);
        }

        [Fact]
        public void GetReserves_Success()
        {
            ulong expectedCrs = 100;
            UInt256 expectedToken = 150;

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), expectedCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), expectedToken);
            
            var pool = CreateNewOpdexStandardPool();

            var reserves = pool.Reserves;
            var reserveCrs = (ulong)reserves[0];
            var reserveToken = (UInt256)reserves[1];
            
            reserveCrs.Should().Be(expectedCrs);
            reserveToken.Should().Be(expectedToken);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Sync_Success(bool authorize)
        {
            var sender = Trader0;
            const ulong expectedBalanceCrs = 100;
            UInt256 expectedBalanceToken = 150;

            var pool = CreateNewOpdexStandardPool(expectedBalanceCrs, authProviders: authorize);

            var authParams = new object[] {sender, (byte)Permissions.Provide};
            if (authorize)
            {
                SetupCall(StandardMarket, 0ul, nameof(IOpdexStandardMarket.IsAuthorized), authParams,
                    TransferResult.Transferred(true));
            }
            
            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStandardPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceToken));
            
            SetupMessage(Pool, sender);
            
            pool.Sync();

            pool.ReserveCrs.Should().Be(expectedBalanceCrs);
            pool.ReserveSrc.Should().Be(expectedBalanceToken);

            VerifyCall(Token, 0ul, nameof(IOpdexStandardPool.GetBalance), expectedSrcBalanceParams, Times.Once);
            
            if (authorize)
            {
                VerifyCall(StandardMarket, 0, nameof(IOpdexStandardMarket.IsAuthorized), authParams, Times.Once);
            }
            
            VerifyLog(new ReservesLog
            {
                ReserveCrs = expectedBalanceCrs, 
                ReserveSrc = expectedBalanceToken
            }, Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Skim_Success(bool authorize)
        {
            var sender = Trader0;
            const ulong expectedBalanceCrs = 100;
            UInt256 expectedBalanceToken = 150;
            const ulong currentReserveCrs = 50;
            UInt256 currentReserveSrc = 100;

            var pool = CreateNewOpdexStandardPool(expectedBalanceCrs, authProviders: authorize);
            
            var authParams = new object[] {sender, sender, (byte)Permissions.Provide};
            if (authorize)
            {
                SetupCall(StandardMarket, 0ul, nameof(IOpdexStandardMarket.IsAuthorized), authParams,
                    TransferResult.Transferred(true));
            }
            
            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStandardPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceToken));

            var expectedTransferToParams = new object[] { sender, (UInt256)50 };
            SetupCall(Token, 0ul, nameof(IOpdexStandardPool.TransferTo), expectedTransferToParams, TransferResult.Transferred(true));

            SetupTransfer(sender, 50ul, TransferResult.Transferred(true));

            SetupMessage(Pool, sender);
            
            pool.Skim(sender);
            
            if (authorize)
            {
                VerifyCall(StandardMarket, 0, nameof(IOpdexStandardMarket.IsAuthorized), authParams, Times.Once);
            }

            VerifyCall(Token, 0ul, nameof(IOpdexStandardPool.GetBalance), expectedSrcBalanceParams, Times.Once);
            VerifyCall( Token, 0ul, nameof(IOpdexStandardPool.TransferTo), expectedTransferToParams, Times.Once);
            VerifyTransfer(sender, 50ul, Times.Once);
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
         
            State.SetUInt256($"Balance:{from}", initialFromBalance);
            State.SetUInt256($"Balance:{to}", initialToBalance);
            SetupMessage(Pool, from);

            var success = pool.TransferTo(to, amount);

            success.Should().BeTrue();
            pool.GetBalance(from).Should().Be(finalFromBalance);
            pool.GetBalance(to).Should().Be(finalToBalance);
            
            VerifyLog(new TransferLog
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
         
            State.SetUInt256($"Balance:{from}", initialFromBalance);
            State.SetUInt256($"Balance:{to}", initialToBalance);
            SetupMessage(Pool, from);

            pool.TransferTo(to, amount).Should().BeFalse();
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
         
            State.SetUInt256($"Balance:{from}", initialFromBalance);
            State.SetUInt256($"Balance:{to}", initialToBalance);
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
            UInt256 amount = 200;
            UInt256 initialFromBalance = 200;
            UInt256 initialToBalance = 50;
            UInt256 initialSpenderAllowance = 200;
            UInt256 finalFromBalance = 0;
            UInt256 finalToBalance = 250;
            UInt256 finalSpenderAllowance = 0;
         
            State.SetUInt256($"Balance:{from}", initialFromBalance);
            State.SetUInt256($"Balance:{to}", initialToBalance);
            State.SetUInt256($"Allowance:{from}:{to}", initialSpenderAllowance);
            SetupMessage(Pool, to);

            pool.TransferFrom(from, to, amount).Should().BeTrue();
            pool.GetBalance(from).Should().Be(finalFromBalance);
            pool.GetBalance(to).Should().Be(finalToBalance);
            pool.Allowance(from, to).Should().Be(finalSpenderAllowance);
            
            VerifyLog(new TransferLog
            {
                From = from, 
                To = to, 
                Amount = amount
            }, Times.Once);
        }

        [Fact]
        public void TransferFrom_Fails_InsufficientFromBalance()
        {
            var pool = CreateNewOpdexStandardPool();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 150;
            UInt256 initialFromBalance = 100;
            UInt256 spenderAllowance = 150;
         
            State.SetUInt256($"Balance:{from}", initialFromBalance);
            State.SetUInt256($"Allowance:{from}:{to}", spenderAllowance);
            SetupMessage(Pool, to);

            pool.TransferFrom(from, to, amount).Should().BeFalse();
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
         
            State.SetUInt256($"Balance:{from}", initialFromBalance);
            State.SetUInt256($"Allowance:{from}:{to}", spenderAllowance);
            SetupMessage(Pool, to);
            
            pool.TransferFrom(from, to, amount).Should().BeFalse();
        }

        [Fact]
        public void Approve_Success()
        {
            var pool = CreateNewOpdexStandardPool();
            var from = Trader0;
            var spender = Trader1;
            UInt256 amount = 100;
            UInt256 allowance = 50;
            
            SetupMessage(Pool, from);
            
            State.SetUInt256($"Allowance:{from}:{spender}", allowance);

            pool.Approve(spender, allowance, amount).Should().BeTrue();
            pool.Allowance(from, spender).Should().Be(amount);
            
            VerifyLog(new ApprovalLog
            {
                Owner = from, 
                Spender = spender, 
                Amount = amount,
                OldAmount = allowance
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
            UInt256 currentTraderBalance = 0;
            UInt256 expectedLiquidity = 435888894;
            UInt256 expectedKLast = 190_000_000_000_000_000;
            UInt256 expectedBurnAmount = 1_000;
            var trader = Trader0;

            var pool = CreateNewOpdexStandardPool(currentBalanceCrs);
            
            SetupMessage(Pool, Trader0);
            
            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);
            State.SetUInt256(nameof(IOpdexStandardPool.TotalSupply), currentTotalSupply);
            State.SetUInt256(nameof(IOpdexStandardPool.KLast), currentKLast);
            State.SetUInt256($"Balance:{trader}", currentTraderBalance);
            
            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStandardPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));

            var mintedLiquidity = pool.Mint(trader);
            mintedLiquidity.Should().Be(expectedLiquidity);
            
            pool.TotalSupply.Should().Be(expectedLiquidity + expectedBurnAmount); // burned
            pool.ReserveCrs.Should().Be(currentBalanceCrs);
            pool.ReserveSrc.Should().Be(currentBalanceToken);

            if (pool.MarketFeeEnabled)
            {
                pool.KLast.Should().Be(expectedKLast);
            }

            var traderBalance = pool.GetBalance(trader);
            traderBalance.Should().Be(expectedLiquidity);
            
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), expectedSrcBalanceParams, Times.Once);

            VerifyLog(new ReservesLog
            {
                ReserveCrs = currentBalanceCrs,
                ReserveSrc = currentBalanceToken
            }, Times.Once);
            
            VerifyLog(new MintLog
            {
                AmountCrs = currentBalanceCrs,
                AmountSrc = currentBalanceToken,
                Sender = trader,
                To = trader,
                AmountLpt = expectedLiquidity
            }, Times.Once);
            
            VerifyLog(new TransferLog
            {
                From = Address.Zero,
                To = Address.Zero,
                Amount = expectedBurnAmount
            }, Times.Once);

            VerifyLog(new TransferLog
            {
                From = Address.Zero,
                To = trader,
                Amount = expectedLiquidity
            }, Times.Once);
        }
        
        [Theory]
        [InlineData(false, 250, 0)]
        [InlineData(true, 252, 21)]
        public void MintWithExistingReserves_Success(bool marketFeeEnabled, UInt256 expectedLiquidity, UInt256 mintedFee)
        {
            const ulong currentBalanceCrs = 5_500ul;
            UInt256 currentBalanceToken = 11_000;
            const ulong currentReserveCrs = 5_000;
            UInt256 currentReserveSrc = 10_000;
            UInt256 currentTotalSupply = 2500;
            UInt256 expectedKLast = 45_000_000;
            UInt256 expectedK = currentBalanceCrs * currentBalanceToken;
            UInt256 currentTraderBalance = 0;
            var trader = Trader0;

            var pool = CreateNewOpdexStandardPool(currentBalanceCrs, marketFeeEnabled: marketFeeEnabled);
            SetupMessage(Pool, trader);
            
            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);
            State.SetUInt256(nameof(IOpdexStandardPool.TotalSupply), currentTotalSupply);
            State.SetUInt256(nameof(IOpdexStandardPool.KLast), expectedKLast);
            State.SetUInt256($"Balance:{Trader0}", currentTraderBalance);

            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStandardPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));
            
            var mintedLiquidity = pool.Mint(Trader0);
            mintedLiquidity.Should().Be(expectedLiquidity);
            
            pool.TotalSupply.Should().Be(currentTotalSupply + expectedLiquidity + mintedFee);
            pool.ReserveCrs.Should().Be(currentBalanceCrs);
            pool.ReserveSrc.Should().Be(currentBalanceToken);

            if (pool.MarketFeeEnabled)
            {
                pool.KLast.Should().Be(expectedK);
            }

            var traderBalance = pool.GetBalance(trader);
            traderBalance.Should().Be(expectedLiquidity);
            
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), expectedSrcBalanceParams, Times.Once);
            
            VerifyLog(new ReservesLog
            {
                ReserveCrs = currentBalanceCrs,
                ReserveSrc = currentBalanceToken
            }, Times.Once);
            
            VerifyLog(new MintLog
            {
                AmountCrs = 500,
                AmountSrc = 1000,
                Sender = trader,
                To = trader,
                AmountLpt = expectedLiquidity
            }, Times.Once);

            VerifyLog(new TransferLog
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
            UInt256 currentTraderBalance = 0;
            var trader = Trader0;

            var pool = CreateNewOpdexStandardPool(currentBalanceCrs);
            
            SetupMessage(Pool, Trader0);
            
            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);
            State.SetUInt256(nameof(IOpdexStandardPool.TotalSupply), currentTotalSupply);
            State.SetUInt256(nameof(IOpdexStandardPool.KLast), currentKLast);
            State.SetUInt256($"Balance:{trader}", currentTraderBalance);
            
            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStandardPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceToken));
            
            pool
                .Invoking(p => p.Mint(trader))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }

        [Fact]
        public void Mint_Throws_ContractLocked()
        {
            var pool = CreateNewOpdexStandardPool();

            SetupMessage(Pool, StandardMarket);
            
            State.SetBool(nameof(IOpdexStandardPool.Locked), true);

            pool
                .Invoking(p => p.Mint(Trader0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }
        
        #endregion
        
        #region Burn Tests
        
        [Theory]
        [InlineData(false, 8_000, 80_000, 0)]
        [InlineData(true, 7_931, 79_317, 129)]
        public void BurnPartialLiquidity_Success(bool marketFeeEnabled, ulong expectedReceivedCrs, UInt256 expectedReceivedSrc, UInt256 expectedMintedFee)
        {
            const ulong currentReserveCrs = 100_000;
            UInt256 currentReserveSrc = 1_000_000;
            UInt256 currentTotalSupply = 15_000;
            UInt256 currentKLast = 90_000_000_000;
            UInt256 burnAmount = 1_200;
            var to = Trader0;

            var pool = CreateNewOpdexStandardPool(currentReserveCrs, marketFeeEnabled: marketFeeEnabled);
            SetupMessage(Pool, StandardMarket);
            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);
            State.SetUInt256(nameof(IOpdexStandardPool.TotalSupply), currentTotalSupply);
            State.SetUInt256($"Balance:{Pool}", burnAmount);
            State.SetUInt256(nameof(IOpdexStandardPool.KLast), currentKLast);
            
            var getBalanceCallParams = new object[] {Pool};
            SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), getBalanceCallParams, TransferResult.Transferred(currentReserveSrc));
            
            SetupTransfer(to, expectedReceivedCrs, TransferResult.Transferred(true));
            
            var transferToParams = new object[] {to, expectedReceivedSrc};
            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), new object[] { to, expectedReceivedSrc }, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), getBalanceCallParams, TransferResult.Transferred(currentReserveSrc - expectedReceivedSrc));
            });

            var results = pool.Burn(to);
            results[0].Should().Be((UInt256)expectedReceivedCrs);
            results[1].Should().Be(expectedReceivedSrc);
            pool.Balance.Should().Be(currentReserveCrs - expectedReceivedCrs);
            pool.TotalSupply.Should().Be(currentTotalSupply - burnAmount + expectedMintedFee);
            pool.GetBalance(StandardMarket).Should().Be(expectedMintedFee);

            if (pool.MarketFeeEnabled)
            {
                pool.KLast.Should().Be((currentReserveCrs - expectedReceivedCrs) * (currentReserveSrc - expectedReceivedSrc));
            }
            
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), getBalanceCallParams, Times.Exactly(2));
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), transferToParams, Times.Once);
            VerifyTransfer(to, expectedReceivedCrs, Times.Once);
            
            // Burn Tokens
            VerifyLog(new TransferLog
            {
                From = Pool,
                To = Address.Zero,
                Amount = burnAmount
            }, Times.Once);
            
            // Burn Log Summary
            VerifyLog(new BurnLog
            {
                Sender = StandardMarket,
                To = to,
                AmountCrs = expectedReceivedCrs,
                AmountSrc = expectedReceivedSrc,
                AmountLpt = burnAmount
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
            SetupMessage(Pool, StandardMarket);
            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);
            State.SetUInt256(nameof(IOpdexStandardPool.TotalSupply), currentTotalSupply);
            State.SetUInt256($"Balance:{Pool}", burnAmount);
            State.SetUInt256(nameof(IOpdexStandardPool.KLast), currentKLast);

            var getBalanceCallParams = new object[] {Pool};
            SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), getBalanceCallParams, TransferResult.Transferred(currentReserveSrc));
            
            SetupTransfer(to, expectedReceivedCrs, TransferResult.Transferred(true));

            var transferToParams = new object[] {to, expectedReceivedSrc};
            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), transferToParams, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), getBalanceCallParams, TransferResult.Transferred(currentReserveSrc - expectedReceivedSrc));
            });

            var results = pool.Burn(to);
            results[0].Should().Be((UInt256)expectedReceivedCrs);
            results[1].Should().Be(expectedReceivedSrc);
            pool.Balance.Should().Be(currentReserveCrs - expectedReceivedCrs);
            pool.TotalSupply.Should().Be(currentTotalSupply + expectedMintedFee - burnAmount);
            
            if (pool.MarketFeeEnabled)
            {
                pool.KLast.Should().Be((currentReserveCrs - expectedReceivedCrs) * (currentReserveSrc - expectedReceivedSrc));
            }

            VerifyCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), getBalanceCallParams, Times.Exactly(2));
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), transferToParams, Times.Once);
            VerifyTransfer(to, expectedReceivedCrs, Times.Once);
            
            // Burn Tokens
            VerifyLog(new TransferLog
            {
                From = Pool,
                To = Address.Zero,
                Amount = burnAmount
            }, Times.Once);
            
            // Burn Log Summary
            VerifyLog(new BurnLog
            {
                Sender = StandardMarket,
                To = to,
                AmountCrs = expectedReceivedCrs,
                AmountSrc = expectedReceivedSrc,
                AmountLpt = burnAmount
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
            SetupMessage(Pool, StandardMarket);
            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);
            State.SetUInt256(nameof(IOpdexStandardPool.TotalSupply), currentTotalSupply);
            State.SetUInt256($"Balance:{Pool}", burnAmount);
            State.SetUInt256(nameof(IOpdexStandardPool.KLast), currentKLast);
            
            SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc));

            pool
                .Invoking(p => p.Burn(to))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY_BURNED");
        }

        [Fact]
        public void Burn_Throws_LockedContract()
        {
            var pool = CreateNewOpdexStandardPool();

            SetupMessage(Pool, StandardMarket);
            
            State.SetBool(nameof(IOpdexStandardPool.Locked), true);

            pool
                .Invoking(p => p.Burn(Trader0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }
        
        #endregion
        
        #region Swap Tests

        [Theory]
        [InlineData(17_000, 450_000, 200_000, 7_259)]
        [InlineData(1_005_016, 100_099_600_698, 9_990_079_661_494, 100_000_000)]
        public void SwapCrsForSrc_Success(ulong swapAmountCrs, ulong currentReserveCrs, UInt256 currentReserveSrc, UInt256 expectedReceivedToken)
        {
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, StandardMarket, swapAmountCrs);

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), new object[] { to, expectedReceivedToken }, TransferResult.Transferred(true));
            SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - expectedReceivedToken));

            pool.Swap(0, expectedReceivedToken, to, new byte[0]);

            pool.ReserveCrs.Should().Be(currentReserveCrs + swapAmountCrs);
            pool.ReserveSrc.Should().Be(currentReserveSrc - expectedReceivedToken);
            pool.Balance.Should().Be(currentReserveCrs + swapAmountCrs);
            
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), new object[] {to, expectedReceivedToken}, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, Times.Once);
            
            VerifyLog(new ReservesLog
            {
                ReserveCrs = currentReserveCrs + swapAmountCrs,
                ReserveSrc = currentReserveSrc - expectedReceivedToken
            }, Times.Once);

            VerifyLog(new SwapLog
            {
                AmountCrsIn = swapAmountCrs,
                AmountCrsOut = 0,
                AmountSrcIn = 0,
                AmountSrcOut = expectedReceivedToken,
                Sender = StandardMarket,
                To = to
            }, Times.Once);
        }

        [Theory]
        [InlineData(2_941, 450_000, 200_000, 6_500)]
        [InlineData(101_004_414, 99_901_400_322, 10_009_900_000_000, 1_005_016)]
        public void SwapSrcForCrs_Success(UInt256 swapAmountSrc, ulong currentReserveCrs, UInt256 currentReserveSrc, ulong expectedCrsReceived)
        {
            var to = Trader0;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);

            SetupMessage(Pool, StandardMarket);
            
            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);
            
            SetupTransfer(to, expectedCrsReceived, TransferResult.Transferred(true));
            SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + swapAmountSrc));

            pool.Swap(expectedCrsReceived, 0, to, new byte[0]);
            
            pool.ReserveCrs.Should().Be(currentReserveCrs - expectedCrsReceived);
            pool.ReserveSrc.Should().Be(currentReserveSrc + swapAmountSrc);
            pool.Balance.Should().Be(currentReserveCrs - expectedCrsReceived);

            VerifyLog(new ReservesLog
            {
                ReserveCrs = currentReserveCrs - expectedCrsReceived,
                ReserveSrc = currentReserveSrc + swapAmountSrc
            }, Times.Once);

            VerifyLog(new SwapLog
            {
                AmountCrsIn = 0,
                AmountCrsOut = expectedCrsReceived,
                AmountSrcIn = swapAmountSrc,
                AmountSrcOut = 0,
                Sender = StandardMarket,
                To = to
            }, Times.Once);
        }

        [Fact]
        public void Swap_Throws_InvalidOutputAmount()
        {
            const ulong currentReserveCrs = 450_000;
            UInt256 currentReserveSrc = 200_000;
            
            var pool = CreateNewOpdexStandardPool(currentReserveCrs);
            SetupMessage(Pool, StandardMarket);
            
            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);
            
            pool
                .Invoking(p => p.Swap(0, 0, Trader0, new byte[0]))
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
            SetupMessage(Pool, StandardMarket);
            
            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);
            
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
            SetupMessage(Pool, StandardMarket);
            
            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);
            
            foreach(var address in addresses)
            {
                State.SetBool(nameof(IOpdexStandardPool.Locked), false);
                
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

            SetupMessage(Pool, StandardMarket);

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), new object[] { to, expectedReceivedToken }, TransferResult.Transferred(true));
            SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - expectedReceivedToken));

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

            SetupMessage(Pool, StandardMarket);
            
            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);
            
            SetupTransfer(to, expectedCrsReceived, TransferResult.Transferred(true));
            SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc));

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

            SetupMessage(Pool, StandardMarket, 1);

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), new object[] { to, expectedReceivedToken }, TransferResult.Transferred(true));
            SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - expectedReceivedToken));

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

            SetupMessage(Pool, StandardMarket);
            
            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);
            
            SetupTransfer(to, expectedCrsReceived, TransferResult.Transferred(true));
            SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + 1));

            pool
                .Invoking(p => p.Swap(expectedCrsReceived, 0, to, new byte[0]))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        }

        [Fact]
        public void Swap_Throws_LockedContract()
        {
            var pool = CreateNewOpdexStandardPool();

            SetupMessage(Pool, StandardMarket);
            
            State.SetBool(nameof(IOpdexStandardPool.Locked), true);

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

            SetupMessage(Pool, StandardMarket);

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), new object[] { to, borrowedSrc }, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + expectedFee));
            });
            
            pool.Swap(0, borrowedSrc, to, Serializer.Serialize(callbackData));

            pool.ReserveCrs.Should().Be(currentReserveCrs);
            pool.ReserveSrc.Should().Be(currentReserveSrc + expectedFee);
            pool.Balance.Should().Be(currentReserveCrs);
            
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), new object[] {to, borrowedSrc}, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, Times.Once);
            VerifyCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, Times.Once);
            
            VerifyLog(new ReservesLog
            {
                ReserveCrs = currentReserveCrs,
                ReserveSrc = currentReserveSrc + expectedFee
            }, Times.Once);

            VerifyLog(new SwapLog
            {
                AmountCrsIn = 0,
                AmountCrsOut = 0,
                AmountSrcIn = 17_052,
                AmountSrcOut = 17_000,
                Sender = StandardMarket,
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

            SetupMessage(Pool, StandardMarket);

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), new object[] { to, borrowedSrc }, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - borrowedSrc));
                SetupBalance(currentReserveCrs + expectedCrsReceived);
            });
            
            pool.Swap(0, borrowedSrc, to, Serializer.Serialize(callbackData));

            pool.ReserveCrs.Should().Be(currentReserveCrs + expectedCrsReceived);
            pool.ReserveSrc.Should().Be(currentReserveSrc - borrowedSrc);
            pool.Balance.Should().Be(currentReserveCrs + expectedCrsReceived);
            
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), new object[] {to, borrowedSrc}, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, Times.Once);
            VerifyCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, Times.Once);
            
            VerifyLog(new ReservesLog
            {
                ReserveCrs = currentReserveCrs + expectedCrsReceived,
                ReserveSrc = currentReserveSrc - borrowedSrc
            }, Times.Once);

            VerifyLog(new SwapLog
            {
                AmountCrsIn = expectedCrsReceived,
                AmountCrsOut = 0,
                AmountSrcIn = 0,
                AmountSrcOut = borrowedSrc,
                Sender = StandardMarket,
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

            SetupMessage(Pool, StandardMarket);

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            SetupTransfer(to, borrowedCrs, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc));
                SetupBalance(currentReserveCrs + expectedCrsFee);
            });
            
            pool.Swap(borrowedCrs, 0, to, Serializer.Serialize(callbackData));

            pool.ReserveCrs.Should().Be(currentReserveCrs + expectedCrsFee);
            pool.ReserveSrc.Should().Be(currentReserveSrc);
            pool.Balance.Should().Be(currentReserveCrs + expectedCrsFee);
            
            VerifyTransfer(to, borrowedCrs, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, Times.Once);
            VerifyCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, Times.Once);
            
            VerifyLog(new ReservesLog
            {
                ReserveCrs = currentReserveCrs + expectedCrsFee,
                ReserveSrc = currentReserveSrc
            }, Times.Once);

            VerifyLog(new SwapLog
            {
                AmountCrsIn = borrowedCrs + expectedCrsFee,
                AmountCrsOut = borrowedCrs,
                AmountSrcIn = 0,
                AmountSrcOut = 0,
                Sender = StandardMarket,
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

            SetupMessage(Pool, StandardMarket);

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            SetupTransfer(to, borrowedCrs, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + expectedSrcReceived));
            });
            
            pool.Swap(borrowedCrs, 0, to, Serializer.Serialize(callbackData));

            pool.ReserveCrs.Should().Be(currentReserveCrs - borrowedCrs);
            pool.ReserveSrc.Should().Be(currentReserveSrc + expectedSrcReceived);
            pool.Balance.Should().Be(currentReserveCrs - borrowedCrs);
            
            VerifyTransfer(to, borrowedCrs, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, Times.Once);
            VerifyCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, Times.Once);
            
            VerifyLog(new ReservesLog
            {
                ReserveCrs = currentReserveCrs - borrowedCrs,
                ReserveSrc = currentReserveSrc + expectedSrcReceived
            }, Times.Once);

            VerifyLog(new SwapLog
            {
                AmountCrsIn = 0,
                AmountCrsOut = borrowedCrs,
                AmountSrcIn = expectedSrcReceived,
                AmountSrcOut = 0,
                Sender = StandardMarket,
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

            SetupMessage(Pool, StandardMarket);

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            SetupTransfer(to, borrowedCrs, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + expectedSrcReceived));
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

            SetupMessage(Pool, StandardMarket);

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), new object[] { to, borrowedSrc }, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - borrowedSrc));
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

            SetupMessage(Pool, StandardMarket);

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            SetupTransfer(to, borrowedCrs, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method, new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + expectedSrcReceived));
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

            SetupMessage(Pool, StandardMarket);

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), new object[] { to, borrowedSrc }, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - borrowedSrc));
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

            SetupMessage(Pool, StandardMarket);

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            SetupTransfer(to, borrowedCrs, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc + expectedSrcReceived));
                SetupBalance(currentReserveCrs - borrowedCrs + expectedCrsReceived);
            });
            
            pool.Swap(borrowedCrs, 0, to, Serializer.Serialize(callbackData));

            pool.ReserveCrs.Should().Be(currentReserveCrs - borrowedCrs + expectedCrsReceived);
            pool.ReserveSrc.Should().Be(currentReserveSrc + expectedSrcReceived);
            pool.Balance.Should().Be(currentReserveCrs - borrowedCrs + expectedCrsReceived);
            
            VerifyTransfer(to, borrowedCrs, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, Times.Once);
            VerifyCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, Times.Once);
            
            VerifyLog(new ReservesLog
            {
                ReserveCrs = currentReserveCrs - borrowedCrs + expectedCrsReceived,
                ReserveSrc = currentReserveSrc + expectedSrcReceived
            }, Times.Once);

            VerifyLog(new SwapLog
            {
                AmountCrsIn = expectedCrsReceived,
                AmountCrsOut = borrowedCrs,
                AmountSrcIn = expectedSrcReceived,
                AmountSrcOut = 0,
                Sender = StandardMarket,
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

            SetupMessage(Pool, StandardMarket);

            State.SetUInt64(nameof(IOpdexStandardPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStandardPool.ReserveSrc), currentReserveSrc);

            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), new object[] { to, borrowedSrc }, TransferResult.Transferred(true));

            var callbackData = new CallbackData {Method = "SomeMethod", Data = Serializer.Serialize("Test")};
            SetupCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - borrowedSrc + expectedSrcReceived));
                SetupBalance(currentReserveCrs + expectedCrsReceived);
            });
            
            pool.Swap(0, borrowedSrc, to, Serializer.Serialize(callbackData));

            pool.ReserveCrs.Should().Be(currentReserveCrs + expectedCrsReceived);
            pool.ReserveSrc.Should().Be(currentReserveSrc - borrowedSrc + expectedSrcReceived);
            pool.Balance.Should().Be(currentReserveCrs + expectedCrsReceived);
            
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.TransferTo), new object[] {to, borrowedSrc}, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.GetBalance), new object[] {Pool}, Times.Once);
            VerifyCall(to, 0, callbackData.Method,  new object[] {callbackData.Data}, Times.Once);
            
            VerifyLog(new ReservesLog
            {
                ReserveCrs = currentReserveCrs + expectedCrsReceived,
                ReserveSrc = currentReserveSrc - borrowedSrc + expectedSrcReceived
            }, Times.Once);

            VerifyLog(new SwapLog
            {
                AmountCrsIn = expectedCrsReceived,
                AmountCrsOut = 0,
                AmountSrcIn = expectedSrcReceived,
                AmountSrcOut = borrowedSrc,
                Sender = StandardMarket,
                To = to
            }, Times.Once);
        }

        #endregion
        
        #region Maintain State Tests

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        public void MaintainState_FeesEnabled_NoAuth(uint fee)
        {
            var pool = CreateNewOpdexStandardPool(fee: fee, marketFeeEnabled: true);
            var router = CreateNewOpdexRouter(StandardMarket, fee);

            // trader 0 add liquidity
            var trader0Liquidity1 = AddLiquidity(pool, Trader0, 100_000_000, 250_000_000);
            
            // trader 0 add more liquidity
            var trader0Liquidity2 = AddLiquidity(pool, Trader0, 100_000_000, 250_000_000);
            
            // trader 1 swap src for crs
            Swap(pool, router, Trader1, 0, 150_000);
            
            // miner 1 swap crs for src
            Swap(pool, router, Miner1, 250_000, 0);

            // miner 1 add liquidity
            var miner1LiquidityAmountSrc = router.GetLiquidityQuote(300_000_000, pool.ReserveCrs, pool.ReserveSrc);
            var miner1Liquidity = AddLiquidity(pool, Miner1, 300_000_000, miner1LiquidityAmountSrc);
            
            // trader 0 remove liquidity
            RemoveLiquidity(pool, Trader0, trader0Liquidity1 + trader0Liquidity2);
            
            // miner 1 remove liquidity
            RemoveLiquidity(pool, Miner1, miner1Liquidity);

            // All have exited, total supply should be 1000 burned plus fees to the market
            pool.TotalSupply.Should().Be(1000 + pool.GetBalance(StandardMarket));
        }

        private UInt256 AddLiquidity(IOpdexStandardPool pool, Address trader, ulong amountCrs, UInt256 amountSrc)
        {
            // Notice we're sending the amountCrs in the message
            SetupMessage(Pool, trader, amountCrs);
            
            // Set balance as the Router contract would have with previous calls in the same transaction
            // Router uses TransferFrom to send SRC Tokens from the trader to the pool
            var srcBalance = pool.ReserveSrc + amountSrc;
            SetupCall(pool.Token, 0, nameof(IStandardToken256.GetBalance), new object[] { Pool }, TransferResult.Transferred(srcBalance));

            // Simulate 1000 burn if TotalSupply is 0
            var currentTotalSupply = pool.TotalSupply == 0 ? 1000 : pool.TotalSupply;
            var currentTraderBalance = pool.GetBalance(trader);
            
            var expectedMintedFee = CalculateFee(pool);
            
            var liquidity = pool.Mint(trader);

            liquidity.Should().NotBe(UInt256.Zero);
            pool.ReserveSrc.Should().Be(srcBalance);
            pool.ReserveCrs.Should().Be(pool.Balance);
            
            if (pool.MarketFeeEnabled)
            {
                pool.KLast.Should().Be(pool.ReserveSrc * pool.ReserveCrs);
            }
            else
            {
                pool.KLast.Should().Be(UInt256.Zero);
            }
            
            pool.TotalSupply.Should().Be(currentTotalSupply + liquidity + expectedMintedFee);
            pool.GetBalance(trader).Should().Be(currentTraderBalance + liquidity);

            VerifyLog(new MintLog { AmountCrs = amountCrs, AmountSrc = amountSrc, AmountLpt = liquidity, Sender = trader, To = trader}, Times.Once);
            VerifyLog(new TransferLog { From = Address.Zero, To = trader, Amount = liquidity}, Times.Once);
            VerifyLog(new ReservesLog { ReserveCrs = pool.Balance, ReserveSrc = srcBalance}, Times.Once);

            return liquidity;
        }

        private void Swap(IOpdexStandardPool pool, IOpdexRouter router, Address trader, ulong amountCrsIn, UInt256 amountSrcIn)
        {
            SetupMessage(Pool, trader, amountCrsIn);

            // SRC in for CRS out
            var amountCrsOut = amountCrsIn == 0 ? (ulong)router.GetAmountOut(amountSrcIn, pool.ReserveSrc, pool.ReserveCrs) : 0;
            
            // CRS in for SRC Out
            var amountSrcOut = amountSrcIn == 0 ? router.GetAmountOut(amountCrsIn, pool.ReserveCrs, pool.ReserveSrc) : 0;
            
            var currentBalance = pool.GetBalance(trader);
            var currentReserveCrs = pool.ReserveCrs;
            var currentReserveSrc = pool.ReserveSrc;
            var currentKLast = pool.KLast;

            SetupTransfer(trader, amountCrsOut, TransferResult.Transferred(true));
            SetupCall(pool.Token, 0, nameof(IStandardToken256.TransferTo), new object[] { trader, amountSrcOut }, TransferResult.Transferred(true));
            var srcBalance = pool.ReserveSrc + amountSrcIn - amountSrcOut;
            SetupCall(pool.Token, 0, nameof(IStandardToken256.GetBalance), new object[] { Pool }, TransferResult.Transferred(srcBalance));
            
            pool.Swap(amountCrsOut, amountSrcOut, trader, new byte[0]);
            pool.ReserveSrc.Should().Be(currentReserveSrc + amountSrcIn - amountSrcOut);
            pool.ReserveCrs.Should().Be(currentReserveCrs + amountCrsIn - amountCrsOut);
            pool.KLast.Should().Be(currentKLast);
            pool.GetBalance(trader).Should().Be(currentBalance);
            
            VerifyLog(new ReservesLog { ReserveCrs = pool.Balance, ReserveSrc = srcBalance}, Times.Once);
            VerifyLog(new SwapLog
            {
                AmountCrsIn = amountCrsIn, AmountCrsOut = amountCrsOut, AmountSrcIn = amountSrcIn, AmountSrcOut = amountSrcOut,
                Sender = trader, To = trader
            }, Times.Once);
        }

        private void RemoveLiquidity(IOpdexStandardPool pool, Address trader, UInt256 amountLpt)
        {
            SetupMessage(Pool, trader);
            
            var currentBalance = pool.GetBalance(trader);
            var currentMarketBalance = pool.GetBalance(StandardMarket);
            var currentReserveCrs = pool.ReserveCrs;
            var currentReserveSrc = pool.ReserveSrc;

            // Set balance as the Router contract would have with previous calls in the same transaction
            // Router uses TransferFrom to send LP Tokens from the trader to the pool
            State.SetUInt256($"Balance:{Pool}", amountLpt);
            State.SetUInt256($"Balance:{trader}", currentBalance - amountLpt);

            // Manually added and calculate the expected minted fee
            var expectedMintedFee = CalculateFee(pool);
            var totalSupplyWithExpectedMintedFee = pool.TotalSupply + expectedMintedFee;
            var expectedAmountCrs = (ulong)(amountLpt * pool.ReserveCrs / totalSupplyWithExpectedMintedFee);
            var expectedAmountSrc = amountLpt * pool.ReserveSrc / totalSupplyWithExpectedMintedFee;
            
            SetupCall(pool.Token, 0, nameof(IStandardToken256.GetBalance), new object[] { Pool }, TransferResult.Transferred(pool.ReserveSrc), () =>
            {
                // Setup second call to get balance after tokens have been transferred to the trader
                SetupCall(pool.Token, 0, nameof(IStandardToken256.GetBalance), new object[] { Pool }, TransferResult.Transferred(pool.ReserveSrc - expectedAmountSrc));
            });
            
            SetupTransfer(trader, expectedAmountCrs, TransferResult.Transferred(true));
            SetupCall(pool.Token, 0, nameof(IStandardToken256.TransferTo), new object[] { trader, expectedAmountSrc }, TransferResult.Transferred(true));

            var tokens = pool.Burn(trader);

            ((ulong)tokens[0]).Should().Be(expectedAmountCrs);
            tokens[1].Should().Be(expectedAmountSrc);
            
            var expectedReserveCrs = currentReserveCrs - expectedAmountCrs;
            var expectedReserveSrc = currentReserveSrc - expectedAmountSrc;

            pool.ReserveSrc.Should().Be(expectedReserveSrc);
            pool.ReserveCrs.Should().Be(expectedReserveCrs);
            pool.GetBalance(trader).Should().Be(currentBalance - amountLpt);
            pool.GetBalance(StandardMarket).Should().Be(currentMarketBalance + expectedMintedFee);
            
            if (pool.MarketFeeEnabled)
            {
                pool.KLast.Should().NotBe(UInt256.Zero);
            }
            else
            {
                pool.KLast.Should().Be(UInt256.Zero);
            }
            
            VerifyLog(new BurnLog { AmountCrs = expectedAmountCrs, AmountSrc = expectedAmountSrc, AmountLpt = amountLpt, Sender = trader, To = trader}, Times.Once);
            VerifyLog(new TransferLog { From = Pool, To = Address.Zero, Amount = amountLpt}, Times.Once);
            VerifyLog(new ReservesLog { ReserveCrs = expectedReserveCrs, ReserveSrc = expectedReserveSrc}, Times.Once);
        }

        private UInt256 CalculateFee(IOpdexStandardPool pool)
        {
            var rootK = Sqrt(pool.ReserveSrc * pool.ReserveCrs);
            var rootKLast = Sqrt(pool.KLast);
            
            var numerator = pool.TotalSupply * (rootK - rootKLast);
            var denominator = (rootK * 5) + rootKLast;
            return numerator / denominator;
        }

        private UInt256 Sqrt(UInt256 value)
        {
            if (value <= 3) return 1;
    
            var result = value;
            var root = (value / 2) + 1;
    
            while (root < result) 
            {
                result = root;
                root = ((value / root) + root) / 2;
            }
    
            return result;
        }

        #endregion
    }
}
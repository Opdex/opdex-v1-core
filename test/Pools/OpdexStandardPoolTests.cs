using System;
using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexV1Core.Tests
{
    public class OpdexStandardPoolTests : TestBase
    {
        [Theory]
        [InlineData(true, false, 0)]
        [InlineData(true, true, 1)]
        [InlineData(true, false, 2)]
        [InlineData(true, false, 3)]
        [InlineData(false, false, 4)]
        [InlineData(true, true, 5)]
        [InlineData(true, false, 6)]
        [InlineData(false, false, 7)]
        [InlineData(true, false, 8)]
        [InlineData(false, true, 9)]
        [InlineData(true, true, 10)]
        public void CreatesNewPool_Success(bool authProviders, bool authTraders, uint fee)
        {
            var pool = CreateNewOpdexStandardPool(authProviders: authProviders, authTraders: authTraders, fee: fee);
            
            pool.Token.Should().Be(Token);
            pool.Decimals.Should().Be(8);
            pool.Name.Should().Be("Opdex Liquidity Pool Token");
            pool.Symbol.Should().Be("OLPT");
            pool.AuthProviders.Should().Be(authProviders);
            pool.AuthTraders.Should().Be(authTraders);
            pool.Fee.Should().Be(fee);
            pool.Market.Should().Be(StandardMarket);
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

        [Theory]
        [InlineData(true, true, (byte)Permissions.Trade, true)]
        [InlineData(false, false, (byte)Permissions.Trade, true)]
        [InlineData(true, false, (byte)Permissions.Trade, true)]
        [InlineData(false, true, (byte)Permissions.Trade, true)]
        [InlineData(true, false, (byte)Permissions.Trade, false)]
        [InlineData(true, true, (byte)Permissions.Provide, true)]
        [InlineData(false, false, (byte)Permissions.Provide, true)]
        [InlineData(true, false, (byte)Permissions.Provide, true)]
        [InlineData(false, true, (byte)Permissions.Provide, true)]
        [InlineData(true, false, (byte)Permissions.Provide, false)]
        public void IsAuthorized_Success(bool shouldAuth, bool isMarketCaller, byte permission, bool expectedResult)
        {
            var pool = CreateNewOpdexStandardPool(authProviders: shouldAuth, authTraders: shouldAuth);

            var sender = isMarketCaller ? StandardMarket : Trader0;
            var authParams = new object[] { sender, permission };
            
            if (shouldAuth && !isMarketCaller)
            {
                SetupCall(StandardMarket, 0, nameof(IOpdexStandardMarket.IsAuthorizedFor), authParams, TransferResult.Transferred(expectedResult));
            }

            var isAuthorized = pool.IsAuthorizedFor(sender, permission);

            isAuthorized.Should().Be(expectedResult);
            
            if (shouldAuth && !isMarketCaller)
            {
                VerifyCall(StandardMarket, 0, nameof(IOpdexStandardMarket.IsAuthorizedFor), authParams, Times.Once);
            }
        }

        [Fact]
        public void SetMarket_Success()
        {
            var pool = CreateNewOpdexStandardPool();
            
            pool.SetMarket(OtherAddress);

            pool.Market.Should().Be(OtherAddress);

            VerifyLog(new MarketChangeLog {From = StandardMarket, To = OtherAddress}, Times.Once);
        }

        [Fact]
        public void SetMarket_Throws_Unauthorized()
        {
            var pool = CreateNewOpdexStandardPool();
            
            SetupMessage(Pool, Trader0);

            pool
                .Invoking(p => p.SetMarket(StandardMarket))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
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
                SetupCall(StandardMarket, 0ul, nameof(IOpdexStandardMarket.IsAuthorizedFor), authParams,
                    TransferResult.Transferred(true));
            }
            
            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStandardPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceToken));
            
            SetupMessage(StandardMarket, sender);
            
            pool.Sync();

            pool.ReserveCrs.Should().Be(expectedBalanceCrs);
            pool.ReserveSrc.Should().Be(expectedBalanceToken);

            VerifyCall(Token, 0ul, nameof(IOpdexStandardPool.GetBalance), expectedSrcBalanceParams, Times.Once);
            
            if (authorize)
            {
                VerifyCall(StandardMarket, 0, nameof(IOpdexStandardMarket.IsAuthorizedFor), authParams, Times.Once);
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
            
            var authParams = new object[] {sender, (byte)Permissions.Provide};
            if (authorize)
            {
                SetupCall(StandardMarket, 0ul, nameof(IOpdexStandardMarket.IsAuthorizedFor), authParams,
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
                VerifyCall(StandardMarket, 0, nameof(IOpdexStandardMarket.IsAuthorizedFor), authParams, Times.Once);
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
            UInt256 amount = 30;
            UInt256 initialFromBalance = 200;
            UInt256 initialToBalance = 50;
            UInt256 initialSpenderAllowance = 100;
            UInt256 finalFromBalance = 170;
            UInt256 finalToBalance = 80;
            UInt256 finalSpenderAllowance = 70;
         
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
        public void TransferFrom_Throws_InsufficientFromBalance()
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
         
            State.SetUInt256($"Balance:{from}", initialFromBalance);
            State.SetUInt256($"Allowance:{from}:{to}", spenderAllowance);
            SetupMessage(Pool, to);
            
            pool
                .Invoking(p => p.TransferFrom(from, to, amount))
                .Should().Throw<OverflowException>();
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
            
            pool.KLast.Should().Be(expectedKLast);
            pool.TotalSupply.Should().Be(expectedLiquidity + expectedBurnAmount); // burned
            pool.ReserveCrs.Should().Be(currentBalanceCrs);
            pool.ReserveSrc.Should().Be(currentBalanceToken);

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
            UInt256 currentTraderBalance = 0;
            var trader = Trader0;

            var pool = CreateNewOpdexStandardPool(currentBalanceCrs);
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
            
            pool.KLast.Should().Be(expectedK);
            pool.TotalSupply.Should().Be(currentTotalSupply + expectedLiquidity);
            pool.ReserveCrs.Should().Be(currentBalanceCrs);
            pool.ReserveSrc.Should().Be(currentBalanceToken);

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
            pool.KLast.Should().Be((currentReserveCrs - expectedReceivedCrs) * (currentReserveSrc - expectedReceivedSrc));
            pool.Balance.Should().Be(currentReserveCrs - expectedReceivedCrs);
            pool.TotalSupply.Should().Be(currentTotalSupply  - burnAmount);
            
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
            pool.KLast.Should().Be((currentReserveCrs - expectedReceivedCrs) * (currentReserveSrc - expectedReceivedSrc));
            pool.Balance.Should().Be(currentReserveCrs - expectedReceivedCrs);
            pool.TotalSupply.Should().Be(currentTotalSupply + expectedMintedFee - burnAmount);

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

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        public void Swap_Throws_InvalidOutputAmount(ulong amountCrsOut, UInt256 amountSrcOut)
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
    }
}
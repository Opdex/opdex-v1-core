using System;
using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexCoreContracts.Tests
{
    public class OpdexStakingPoolTests : TestBase
    {
        [Fact]
        public void CreateNewOpdexStakingPool_Success()
        {
            var pool = CreateNewOpdexStakingPool();

            pool.Token.Should().Be(Token);
            pool.StakeToken.Should().Be(StakeToken);
            pool.Decimals.Should().Be(8);
            pool.Name.Should().Be("Opdex Liquidity Pool Token");
            pool.Symbol.Should().Be("OLPT");
        }

        [Fact]
        public void Sync_Success()
        {
            const ulong expectedBalanceCrs = 100;
            UInt256 expectedBalanceSrc = 150;

            var pool = CreateNewOpdexStakingPool(expectedBalanceCrs);
            
            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceSrc));

            pool.Sync();

            pool.ReserveCrs.Should().Be(expectedBalanceCrs);
            pool.ReserveSrc.Should().Be(expectedBalanceSrc);

            VerifyCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, Times.Once);
            VerifyLog(new ReservesLog
            {
                ReserveCrs = expectedBalanceCrs, 
                ReserveSrc = expectedBalanceSrc
            }, Times.Once);
        }

        [Fact]
        public void Skim_Success()
        {
            const ulong expectedBalanceCrs = 100;
            UInt256 expectedBalanceSrc = 150;
            const ulong currentReserveCrs = 50;
            UInt256 currentReserveSrc = 100;

            var pool = CreateNewOpdexStakingPool(expectedBalanceCrs);
            
            State.SetUInt64(nameof(IOpdexStakingPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStakingPool.ReserveSrc), currentReserveSrc);

            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceSrc));

            var expectedTransferToParams = new object[] { Trader0, (UInt256)50 };
            SetupCall(Token, 0ul, nameof(IOpdexStakingPool.TransferTo), expectedTransferToParams, TransferResult.Transferred(true));

            SetupTransfer(Trader0, 50ul, TransferResult.Transferred(true));
            
            pool.Skim(Trader0);

            VerifyCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, Times.Once);
            VerifyCall(Token, 0ul, nameof(IOpdexStakingPool.TransferTo), expectedTransferToParams, Times.Once);
            VerifyTransfer(Trader0, 50ul, Times.Once);
        }
        
        #region Liquidity Pool Token Tests

        [Fact]
        public void TransferTo_Success()
        {
            var pool = CreateNewOpdexStakingPool();
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
            var pool = CreateNewOpdexStakingPool();
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
            var pool = CreateNewOpdexStakingPool();
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
            var pool = CreateNewOpdexStakingPool();
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
            var pool = CreateNewOpdexStakingPool();
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
            var pool = CreateNewOpdexStakingPool();
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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Approve_Success(bool isIStandardContractImplementation)
        {
            var pool = CreateNewOpdexStakingPool();
            var from = Trader0;
            var spender = Trader1;
            UInt256 amount = 100;
            UInt256 allowance = 10;
            
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
            UInt256 currentBalanceSrc = 1_900_000_000;
            const ulong currentReserveCrs = 0;
            UInt256 currentReserveSrc = 0;
            UInt256 currentTotalSupply = 0;
            UInt256 currentKLast = 0;
            UInt256 currentTraderBalance = 0;
            UInt256 expectedLiquidity = 435888894;
            UInt256 expectedKLast = 190_000_000_000_000_000;
            UInt256 expectedBurnAmount = 1_000;
            var trader = Trader0;

            var pool = CreateNewOpdexStakingPool(currentBalanceCrs);
            
            SetupMessage(Pool, Trader0);
            
            State.SetUInt64(nameof(IOpdexStakingPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStakingPool.ReserveSrc), currentReserveSrc);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalSupply), currentTotalSupply);
            State.SetUInt256(nameof(IOpdexStakingPool.KLast), currentKLast);
            State.SetUInt256($"Balance:{trader}", currentTraderBalance);
            
            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceSrc));

            var mintedLiquidity = pool.Mint(trader);
            mintedLiquidity.Should().Be(expectedLiquidity);
            
            pool.KLast.Should().Be(expectedKLast);
            pool.TotalSupply.Should().Be(expectedLiquidity + expectedBurnAmount); // burned
            pool.ReserveCrs.Should().Be(currentBalanceCrs);
            pool.ReserveSrc.Should().Be(currentBalanceSrc);

            var traderBalance = pool.GetBalance(trader);
            traderBalance.Should().Be(expectedLiquidity);

            VerifyLog(new ReservesLog
            {
                ReserveCrs = currentBalanceCrs,
                ReserveSrc = currentBalanceSrc
            }, Times.Once);
            
            VerifyLog(new MintLog
            {
                AmountCrs = currentBalanceCrs,
                AmountSrc = currentBalanceSrc,
                Sender = trader,
                To = trader
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
            UInt256 currentBalanceSrc = 11_000;
            const ulong currentReserveCrs = 5_000;
            UInt256 currentReserveSrc = 10_000;
            UInt256 currentTotalSupply = 2500;
            UInt256 expectedLiquidity = 252;
            UInt256 expectedKLast = 45_000_000;
            UInt256 expectedK = currentBalanceCrs * currentBalanceSrc;
            UInt256 currentTraderBalance = 0;
            UInt256 mintedFee = 21;
            var trader = Trader0;

            var pool = CreateNewOpdexStakingPool(currentBalanceCrs);
            SetupMessage(Pool, trader);
            
            State.SetUInt64(nameof(IOpdexStakingPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStakingPool.ReserveSrc), currentReserveSrc);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalSupply), currentTotalSupply);
            State.SetUInt256(nameof(IOpdexStakingPool.KLast), expectedKLast);
            State.SetUInt256($"Balance:{Trader0}", currentTraderBalance);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalStaked), 123);
            State.SetAddress(nameof(IOpdexStakingPool.StakeToken), StakeToken);

            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceSrc));
            
            var mintedLiquidity = pool.Mint(Trader0);
            mintedLiquidity.Should().Be(expectedLiquidity);
            
            pool.KLast.Should().Be(expectedK);
            pool.TotalSupply.Should().Be(currentTotalSupply + expectedLiquidity + mintedFee);
            pool.ReserveCrs.Should().Be(currentBalanceCrs);
            pool.ReserveSrc.Should().Be(currentBalanceSrc);

            var traderBalance = pool.GetBalance(trader);
            traderBalance.Should().Be(expectedLiquidity);
            
            VerifyLog(new ReservesLog
            {
                ReserveCrs = currentBalanceCrs,
                ReserveSrc = currentBalanceSrc
            }, Times.Once);
            
            VerifyLog(new MintLog
            {
                AmountCrs = 500,
                AmountSrc = 1000,
                Sender = trader,
                To = trader
            }, Times.Once);
            
            VerifyLog(new TransferLog
            {
                From = Address.Zero,
                To = Pool,
                Amount = mintedFee
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
            UInt256 currentBalanceSrc = 1000;
            const ulong currentReserveCrs = 0;
            UInt256 currentReserveSrc = 0;
            UInt256 currentTotalSupply = 0;
            UInt256 currentKLast = 0;
            UInt256 currentTraderBalance = 0;
            var trader = Trader0;

            var pool = CreateNewOpdexStakingPool(currentBalanceCrs);
            
            SetupMessage(Pool, Trader0);
            
            State.SetUInt64(nameof(IOpdexStakingPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStakingPool.ReserveSrc), currentReserveSrc);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalSupply), currentTotalSupply);
            State.SetUInt256(nameof(IOpdexStakingPool.KLast), currentKLast);
            State.SetUInt256($"Balance:{trader}", currentTraderBalance);
            
            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceSrc));
            
            pool
                .Invoking(p => p.Mint(trader))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }

        [Fact]
        public void Mint_Throws_ContractLocked()
        {
            var pool = CreateNewOpdexStakingPool();

            SetupMessage(Pool, Controller);
            
            State.SetBool(nameof(IOpdexStakingPool.Locked), true);

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
            const ulong expectedReceivedCrs = 7_931;
            UInt256 expectedReceivedSrc = 79_317;
            UInt256 expectedMintedFee = 129;
            var to = Trader0;

            var pool = CreateNewOpdexStakingPool(currentReserveCrs);
            SetupMessage(Pool, Controller);
            State.SetUInt64(nameof(IOpdexStakingPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStakingPool.ReserveSrc), currentReserveSrc);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalSupply), currentTotalSupply);
            State.SetUInt256($"Balance:{Pool}", burnAmount);
            State.SetUInt256(nameof(IOpdexStakingPool.KLast), currentKLast);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalStaked), 123);
            State.SetAddress(nameof(IOpdexStakingPool.StakeToken), StakeToken);
            
            SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc));
            SetupTransfer(to, expectedReceivedCrs, TransferResult.Transferred(true));
            SetupCall(Token, 0, nameof(IOpdexStakingPool.TransferTo), new object[] { to, expectedReceivedSrc }, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - expectedReceivedSrc));
            });

            var results = pool.Burn(to);
            results[0].Should().Be((UInt256)expectedReceivedCrs);
            results[1].Should().Be(expectedReceivedSrc);
            pool.KLast.Should().Be((currentReserveCrs - expectedReceivedCrs) * (currentReserveSrc - expectedReceivedSrc));
            pool.Balance.Should().Be(currentReserveCrs - expectedReceivedCrs);
            pool.TotalSupply.Should().Be(currentTotalSupply + expectedMintedFee - burnAmount);

            // Mint Fee
            VerifyLog(new TransferLog
            {
                From = Address.Zero,
                To = Pool,
                Amount = expectedMintedFee
            }, Times.Once);
            
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

            var pool = CreateNewOpdexStakingPool(currentReserveCrs);
            SetupMessage(Pool, Controller);
            State.SetUInt64(nameof(IOpdexStakingPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStakingPool.ReserveSrc), currentReserveSrc);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalSupply), currentTotalSupply);
            State.SetUInt256($"Balance:{Pool}", burnAmount);
            State.SetUInt256(nameof(IOpdexStakingPool.KLast), currentKLast);
            
            SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc));
            SetupTransfer(to, expectedReceivedCrs, TransferResult.Transferred(true));
            SetupCall(Token, 0, nameof(IOpdexStakingPool.TransferTo), new object[] { to, expectedReceivedSrc }, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - expectedReceivedSrc));
            });

            var results = pool.Burn(to);
            results[0].Should().Be((UInt256)expectedReceivedCrs);
            results[1].Should().Be(expectedReceivedSrc);
            pool.KLast.Should().Be((currentReserveCrs - expectedReceivedCrs) * (currentReserveSrc - expectedReceivedSrc));
            pool.Balance.Should().Be(currentReserveCrs - expectedReceivedCrs);
            pool.TotalSupply.Should().Be(currentTotalSupply + expectedMintedFee - burnAmount);

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

            var pool = CreateNewOpdexStakingPool(currentReserveCrs);
            SetupMessage(Pool, Controller);
            State.SetUInt64(nameof(IOpdexStakingPool.ReserveCrs), currentReserveCrs);
            State.SetUInt256(nameof(IOpdexStakingPool.ReserveSrc), currentReserveSrc);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalSupply), currentTotalSupply);
            State.SetUInt256($"Balance:{Pool}", burnAmount);
            State.SetUInt256(nameof(IOpdexStakingPool.KLast), currentKLast);
            
            SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc));

            pool
                .Invoking(p => p.Burn(to))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY_BURNED");
        }

        [Fact]
        public void Burn_Throws_LockedContract()
        {
            var pool = CreateNewOpdexStakingPool();

            SetupMessage(Pool, Controller);
            
            State.SetBool(nameof(IOpdexStakingPool.Locked), true);

            pool
                .Invoking(p => p.Burn(Trader0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }
        
        #endregion
        
        #region Staking

        [Fact]
        public void Stake_ZeroStakingBalance_Success()
        {
            UInt256 stakeAmount = 1_000;
            
            var pool = CreateNewOpdexStakingPool();
            
            State.SetAddress(nameof(IOpdexStakingPool.StakeToken), StakeToken);
            State.SetUInt256(nameof(IOpdexStakingPool.ReserveSrc), UInt256.Zero);
            State.SetUInt64(nameof(IOpdexStakingPool.ReserveCrs), 0);
            State.SetUInt256(nameof(IOpdexStakingPool.KLast), 0);
            State.SetUInt256($"StakedBalance:{Trader0}", 0);

            var transferFromParameters = new object[] { Trader0, Pool, stakeAmount };
            SetupCall(StakeToken, 0ul, nameof(IOpdexStakingPool.TransferFrom), transferFromParameters, TransferResult.Transferred(true));
            
            SetupMessage(Pool, Trader0);
            
            pool.Stake(stakeAmount);

            pool.TotalStaked.Should().Be(stakeAmount);
            pool.TotalStakedApplicable.Should().Be(UInt256.Zero);
            pool.GetStakedWeight(Trader0).Should().Be(UInt256.Zero);
            pool.GetStakedBalance(Trader0).Should().Be(stakeAmount);
            
            VerifyCall(StakeToken, 0ul, nameof(IOpdexStakingPool.TransferFrom), transferFromParameters, Times.Once);
            VerifyCall(StakeToken, 0ul, "NominateLiquidityPool", null, Times.Once);

            VerifyLog(new EnterStakingPoolLog
            {
                Staker = Trader0,
                Amount = stakeAmount,
                Weight = UInt256.Zero
            }, Times.Once);
        }

        [Fact]
        public void Stake_ZeroTraderBalance_Success()
        {
            UInt256 stakeAmount = 1_000;
            UInt256 reserveSrc = 23_532_234_235;
            const ulong reserveCrs = 2_343_485;
            UInt256 kLast = 55_147_432_946_208_975;
            UInt256 totalSupply = 1_000_000_000;
            UInt256 totalStaked = 10_000;
            UInt256 stakingRewardsBalance = 1_000_000;
            UInt256 expectedWeight = 100_000;
            
            var pool = CreateNewOpdexStakingPool();
            
            State.SetAddress(nameof(IOpdexStakingPool.StakeToken), StakeToken);
            State.SetUInt256(nameof(IOpdexStakingPool.ReserveSrc), reserveSrc);
            State.SetUInt64(nameof(IOpdexStakingPool.ReserveCrs), reserveCrs);
            State.SetUInt256(nameof(IOpdexStakingPool.KLast), kLast);
            State.SetUInt256($"StakedBalance:{Trader0}", 0);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalStaked), totalStaked);
            State.SetUInt256(nameof(IOpdexStakingPool.StakingRewardsBalance), stakingRewardsBalance);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalSupply), totalSupply);

            var transferFromParameters = new object[] { Trader0, Pool, stakeAmount };
            SetupCall(StakeToken, 0ul, nameof(IOpdexStakingPool.TransferFrom), transferFromParameters, TransferResult.Transferred(true));
            
            SetupMessage(Pool, Trader0);
            
            pool.Stake(stakeAmount);

            pool.TotalStaked.Should().Be(stakeAmount + totalStaked);
            pool.TotalStakedApplicable.Should().Be(totalStaked);
            pool.GetStakedWeight(Trader0).Should().Be(expectedWeight);
            pool.GetStakedBalance(Trader0).Should().Be(stakeAmount);
            
            VerifyCall(StakeToken, 0ul, nameof(IOpdexStakingPool.TransferFrom), transferFromParameters, Times.Once);
            VerifyCall(StakeToken, 0ul, "NominateLiquidityPool", null, Times.Once);

            VerifyLog(new EnterStakingPoolLog
            {
                Staker = Trader0,
                Amount = stakeAmount,
                Weight = expectedWeight
            }, Times.Once);
        }

        [Fact]
        public void Stake_AddToTraderBalance_Success()
        {
            const ulong reserveCrs = 2_343_485;
            UInt256 reserveSrc = 23_532_234_235;
            UInt256 stakeAmount = 1_000;
            UInt256 kLast = 55_147_432_946_208_975;
            UInt256 totalSupply = 1_000_000_000;
            UInt256 totalStaked = 10_000;
            UInt256 stakingRewardsBalance = 1_000_000;
            UInt256 expectedWeight = 200_001;
            UInt256 currentStakerBalance = 1_000;
            
            var pool = CreateNewOpdexStakingPool();
            
            State.SetAddress(nameof(IOpdexStakingPool.StakeToken), StakeToken);
            State.SetUInt256(nameof(IOpdexStakingPool.ReserveSrc), reserveSrc);
            State.SetUInt64(nameof(IOpdexStakingPool.ReserveCrs), reserveCrs);
            State.SetUInt256(nameof(IOpdexStakingPool.KLast), kLast);
            State.SetUInt256($"StakedBalance:{Trader0}", currentStakerBalance);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalStaked), totalStaked);
            State.SetUInt256(nameof(IOpdexStakingPool.StakingRewardsBalance), stakingRewardsBalance);
            State.SetUInt256($"Balance:{Pool}", stakingRewardsBalance);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalSupply), totalSupply);

            var transferFromParameters = new object[] { Trader0, Pool, stakeAmount };
            SetupCall(StakeToken, 0ul, nameof(IOpdexStakingPool.TransferFrom), transferFromParameters, TransferResult.Transferred(true));
            
            SetupMessage(Pool, Trader0);
            
            pool.Stake(stakeAmount);

            pool.TotalStaked.Should().Be(totalStaked + currentStakerBalance);
            pool.TotalStakedApplicable.Should().Be(totalStaked - stakeAmount);
            pool.GetStakedWeight(Trader0).Should().Be(expectedWeight);
            pool.GetStakedBalance(Trader0).Should().Be(stakeAmount + currentStakerBalance);
            
            VerifyCall(StakeToken, 0ul, nameof(IOpdexStakingPool.TransferFrom), transferFromParameters, Times.Once);
            VerifyCall(StakeToken, 0ul, "NominateLiquidityPool", null, Times.Once);

            VerifyLog(new CollectStakingRewardsLog
            {
                Staker = Trader0,
                Amount = currentStakerBalance,
                Reward = 100_000
            }, Times.Once);
            
            VerifyLog(new EnterStakingPoolLog
            {
                Staker = Trader0,
                Amount = (stakeAmount + currentStakerBalance),
                Weight = expectedWeight
            }, Times.Once);
        }

        [Fact]
        public void Collect_Burn_Success()
        {
            const ulong reserveCrs = 2_343_485;
            UInt256 reserveSrc = 23_532_234_235;
            UInt256 kLast = 55_147_432_946_208_975;
            UInt256 totalSupply = 1_000_000_000;
            UInt256 totalStaked = 10_000;
            UInt256 stakingRewardsBalance = 1_000_000;
            UInt256 expectedWeight = 100_000;
            UInt256 currentStakerBalance = 1_000;
            UInt256 expectedReward = 100_000;
            const ulong expectedRewardCrs = 234;
            UInt256 expectedRewardSrc = 2_353_223;
            
            var pool = CreateNewOpdexStakingPool();
            
            State.SetAddress(nameof(IOpdexStakingPool.StakeToken), StakeToken);
            State.SetUInt256(nameof(IOpdexStakingPool.ReserveSrc), reserveSrc);
            State.SetUInt64(nameof(IOpdexStakingPool.ReserveCrs), reserveCrs);
            State.SetUInt256(nameof(IOpdexStakingPool.KLast), kLast);
            State.SetUInt256($"StakedBalance:{Trader0}", currentStakerBalance);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalStaked), totalStaked);
            State.SetUInt256(nameof(IOpdexStakingPool.StakingRewardsBalance), stakingRewardsBalance);
            State.SetUInt256($"Balance:{Pool}", stakingRewardsBalance);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalSupply), totalSupply);
            
            SetupMessage(Pool, Trader0);
            SetupBalance(reserveCrs);

            SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(reserveSrc));
            SetupTransfer(Trader0, expectedRewardCrs, TransferResult.Transferred(true));
            SetupCall(Token, 0, nameof(IOpdexStakingPool.TransferTo), new object[] { Trader0, expectedRewardSrc }, TransferResult.Transferred(true));
            
            pool.Collect(Trader0, true);
            
            pool.TotalStaked.Should().Be(totalStaked);
            pool.TotalStakedApplicable.Should().Be(totalStaked - currentStakerBalance);
            pool.GetStakedWeight(Trader0).Should().Be(expectedWeight);
            pool.GetStakedBalance(Trader0).Should().Be(currentStakerBalance);
            pool.GetBalance(Trader0).Should().Be(UInt256.Zero);

            VerifyLog(new TransferLog
            {
                Amount = expectedReward,
                From = Pool,
                To = Address.Zero
            }, Times.Once);
            
            VerifyLog(new BurnLog
            {
                Sender = Trader0,
                To = Trader0,
                AmountCrs = expectedRewardCrs,
                AmountSrc = expectedRewardSrc
            }, Times.Once);
            
            VerifyLog(new CollectStakingRewardsLog
            {
                Staker = Trader0,
                Amount = currentStakerBalance,
                Reward = expectedReward
            }, Times.Once);
        }
        
        [Fact]
        public void Collect_DontBurn_Success()
        {
            const ulong reserveCrs = 2_343_485;
            UInt256 reserveSrc = 23_532_234_235;
            UInt256 kLast = 55_147_432_946_208_975;
            UInt256 totalSupply = 1_000_000_000;
            UInt256 totalStaked = 10_000;
            UInt256 stakingRewardsBalance = 1_000_000;
            UInt256 expectedWeight = 100_000;
            UInt256 currentStakerBalance = 1_000;
            UInt256 expectedReward = 100_000;
            
            var pool = CreateNewOpdexStakingPool();
            
            State.SetAddress(nameof(IOpdexStakingPool.StakeToken), StakeToken);
            State.SetUInt256(nameof(IOpdexStakingPool.ReserveSrc), reserveSrc);
            State.SetUInt64(nameof(IOpdexStakingPool.ReserveCrs), reserveCrs);
            State.SetUInt256(nameof(IOpdexStakingPool.KLast), kLast);
            State.SetUInt256($"StakedBalance:{Trader0}", currentStakerBalance);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalStaked), totalStaked);
            State.SetUInt256(nameof(IOpdexStakingPool.StakingRewardsBalance), stakingRewardsBalance);
            State.SetUInt256($"Balance:{Pool}", stakingRewardsBalance);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalSupply), totalSupply);
            
            SetupMessage(Pool, Trader0);

            pool.Collect(Trader0, false);
            
            pool.TotalStaked.Should().Be(totalStaked);
            pool.TotalStakedApplicable.Should().Be(totalStaked - currentStakerBalance);
            pool.GetStakedWeight(Trader0).Should().Be(expectedWeight);
            pool.GetStakedBalance(Trader0).Should().Be(currentStakerBalance);
            pool.GetBalance(Trader0).Should().Be(expectedReward);
            
            VerifyLog(new CollectStakingRewardsLog
            {
                Staker = Trader0,
                Amount = currentStakerBalance,
                Reward = expectedReward
            }, Times.Once);
        }

        [Fact]
        public void Unstake_AndBurn_Success()
        {
            
        }
        
        [Fact]
        public void Unstake_DontBurn_Success()
        {
            const ulong reserveCrs = 2_343_485;
            UInt256 reserveSrc = 23_532_234_235;
            UInt256 kLast = 55_147_432_946_208_975;
            UInt256 totalSupply = 1_000_000_000;
            UInt256 totalStaked = 10_000;
            UInt256 stakingRewardsBalance = 1_000_000;
            UInt256 currentStakerBalance = 1_000;
            UInt256 expectedReward = 100_000;
            
            var pool = CreateNewOpdexStakingPool();
            
            State.SetAddress(nameof(IOpdexStakingPool.StakeToken), StakeToken);
            State.SetUInt256(nameof(IOpdexStakingPool.ReserveSrc), reserveSrc);
            State.SetUInt64(nameof(IOpdexStakingPool.ReserveCrs), reserveCrs);
            State.SetUInt256(nameof(IOpdexStakingPool.KLast), kLast);
            State.SetUInt256($"StakedBalance:{Trader0}", currentStakerBalance);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalStaked), totalStaked);
            State.SetUInt256(nameof(IOpdexStakingPool.StakingRewardsBalance), stakingRewardsBalance);
            State.SetUInt256($"Balance:{Pool}", stakingRewardsBalance);
            State.SetUInt256(nameof(IOpdexStakingPool.TotalSupply), totalSupply);
            
            SetupMessage(Pool, Trader0);

            var transferToParams = new object[] {Trader0, totalStaked};
            SetupCall(StakeToken, 0ul, nameof(IOpdexStakingPool.TransferTo), transferToParams, TransferResult.Transferred(true));

            pool.Unstake(Trader0, false);
            
            pool.TotalStaked.Should().Be(totalStaked - currentStakerBalance);
            pool.TotalStakedApplicable.Should().Be(totalStaked - currentStakerBalance);
            pool.GetStakedWeight(Trader0).Should().Be(UInt256.Zero);
            pool.GetStakedBalance(Trader0).Should().Be(UInt256.Zero);
            pool.GetBalance(Trader0).Should().Be(expectedReward);

            VerifyCall(StakeToken, 0ul, nameof(IOpdexStakingPool.TransferTo), transferToParams, Times.Once);
            VerifyCall(StakeToken, 0ul, "NominateLiquidityPool", null, Times.Once);
            
            VerifyLog(new CollectStakingRewardsLog
            {
                Staker = Trader0,
                Amount = currentStakerBalance,
                Reward = expectedReward
            }, Times.Once);
        }
        
        #endregion
    }
}
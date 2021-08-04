using System;
using FluentAssertions;
using Moq;
using OpdexV1Core.Tests.Base;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexV1Core.Tests.Pools
{
    public class OpdexStakingPoolTests : TestBase
    {
        private const string NominateLiquidityPoolMethod = "NominateLiquidityPool";

        [Fact]
        public void CreateNewOpdexStakingPool_Success()
        {
            var pool = CreateNewOpdexStakingPool();

            pool.Token.Should().Be(Token);
            pool.StakingToken.Should().Be(StakingToken);
            pool.Decimals.Should().Be(8);
            pool.Name.Should().Be("Opdex Liquidity Pool Token");
            pool.Symbol.Should().Be("OLPT");
            pool.MiningPool.Should().Be(MiningPool1);
        }

        [Fact]
        public void CreateStakingPool_Throws_InvalidMiningPool()
        {
            SetupBalance(0);
            SetupBlock(10);
            SetupMessage(Pool, Owner);

            SetupCreate<OpdexMiningPool>(CreateResult.Failed(), 0ul, new object[] {StakingToken, Pool});

            this.Invoking(p => p.BlankStakingPool(3))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_MINING_POOL");
        }

        [Fact]
        public void CreateStakingPool_EqualTokenAndStakingToken_ZeroAddressMiningPool()
        {
            const uint fee = 3;

            SetupBalance(0);
            SetupBlock(10);
            SetupMessage(Pool, Owner);

            SetupCreate<OpdexMiningPool>(CreateResult.Failed(), 0ul, new object[] {StakingToken, Pool});

            var pool = BlankStakingPool(fee, StakingToken, StakingToken);

            pool.MiningPool.Should().Be(Address.Zero);
        }

        #region Sync Tests

        [Fact]
        public void Sync_NoStakedFees_Success()
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
        public void Sync_WithStakedFees_NoStakers_Success()
        {
            const ulong expectedBalanceCrs = 100;
            UInt256 expectedBalanceSrc = 150;
            UInt256 poolBalance = 100;
            UInt256 stakedRewardsBalance = 100;

            var pool = CreateNewOpdexStakingPool(expectedBalanceCrs);

            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", poolBalance);
            State.SetUInt256(PoolStateKeys.StakingRewardsBalance, stakedRewardsBalance);

            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceSrc));

            pool.Sync();

            pool.ReserveCrs.Should().Be(expectedBalanceCrs);
            pool.ReserveSrc.Should().Be(expectedBalanceSrc);
            pool.StakingRewardsBalance.Should().Be(stakedRewardsBalance);

            VerifyCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, Times.Once);
            VerifyLog(new ReservesLog
            {
                ReserveCrs = expectedBalanceCrs,
                ReserveSrc = expectedBalanceSrc
            }, Times.Once);
        }

        [Fact]
        public void Sync_WithStakedFees_WithStakers_Success()
        {
            const ulong expectedBalanceCrs = 100;
            UInt256 expectedBalanceSrc = 150;
            UInt256 totalStaked = 25;
            UInt256 stakedRewardsBalance = 100;
            UInt256 expectedLatestFees = 100;

            var pool = CreateNewOpdexStakingPool(expectedBalanceCrs);

            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", stakedRewardsBalance);
            State.SetUInt256(PoolStateKeys.TotalStaked, totalStaked);

            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceSrc));

            pool.Sync();

            pool.ReserveCrs.Should().Be(expectedBalanceCrs);
            pool.ReserveSrc.Should().Be(expectedBalanceSrc);
            pool.StakingRewardsBalance.Should().Be(stakedRewardsBalance);
            pool.ApplicableStakingRewards.Should().Be(expectedLatestFees);
            pool.TotalStaked.Should().Be(totalStaked);

            VerifyCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, Times.Once);
            VerifyLog(new ReservesLog
            {
                ReserveCrs = expectedBalanceCrs,
                ReserveSrc = expectedBalanceSrc
            }, Times.Once);
        }

        #endregion

        #region Skim Tests

        [Fact]
        public void Skim_NoStakingRewardsDifference_Success()
        {
            const ulong expectedBalanceCrs = 100;
            UInt256 expectedBalanceSrc = 150;
            const ulong currentReserveCrs = 50;
            UInt256 currentReserveSrc = 100;

            var pool = CreateNewOpdexStakingPool(expectedBalanceCrs);

            State.SetUInt64(PoolStateKeys.ReserveCrs, currentReserveCrs);
            State.SetUInt256(PoolStateKeys.ReserveSrc, currentReserveSrc);

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

        [Fact]
        public void Skim_StakingRewardsDifference_Success()
        {
            const ulong expectedBalanceCrs = 100;
            UInt256 expectedBalanceSrc = 150;
            const ulong currentReserveCrs = 50;
            UInt256 currentReserveSrc = 100;
            UInt256 currentContractBalance = 125;
            UInt256 stakingTokenRewardsBalance = 100;
            UInt256 expectedStakingRewardDifference = 25;

            var pool = CreateNewOpdexStakingPool(expectedBalanceCrs);

            State.SetUInt64(PoolStateKeys.ReserveCrs, currentReserveCrs);
            State.SetUInt256(PoolStateKeys.ReserveSrc, currentReserveSrc);
            State.SetUInt256(PoolStateKeys.StakingRewardsBalance, stakingTokenRewardsBalance);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", currentContractBalance);

            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(expectedBalanceSrc));

            var expectedTransferToParams = new object[] { Trader0, (UInt256)50 };
            SetupCall(Token, 0ul, nameof(IOpdexStakingPool.TransferTo), expectedTransferToParams, TransferResult.Transferred(true));

            SetupTransfer(Trader0, 50ul, TransferResult.Transferred(true));

            pool.Skim(Trader0);
            pool.GetBalance(Pool).Should().Be(stakingTokenRewardsBalance);

            VerifyCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, Times.Once);
            VerifyCall(Token, 0ul, nameof(IOpdexStakingPool.TransferTo), expectedTransferToParams, Times.Once);
            VerifyTransfer(Trader0, 50ul, Times.Once);

            VerifyLog(new TransferLog { From = Pool, To = Trader0, Amount = expectedStakingRewardDifference}, Times.Once);
        }

        #endregion

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

            State.SetUInt256($"{PoolStateKeys.Balance}:{from}", initialFromBalance);
            State.SetUInt256($"{PoolStateKeys.Balance}:{to}", initialToBalance);
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
        public void TransferTo_Fails_InsufficientFromBalance()
        {
            var pool = CreateNewOpdexStakingPool();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 115;
            UInt256 initialFromBalance = 100;
            UInt256 initialToBalance = 0;

            State.SetUInt256($"{PoolStateKeys.Balance}:{from}", initialFromBalance);
            State.SetUInt256($"{PoolStateKeys.Balance}:{to}", initialToBalance);
            SetupMessage(Pool, from);

            pool.TransferTo(to, amount).Should().BeFalse();
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

            State.SetUInt256($"{PoolStateKeys.Balance}:{from}", initialFromBalance);
            State.SetUInt256($"{PoolStateKeys.Balance}:{to}", initialToBalance);
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

            State.SetUInt256($"{PoolStateKeys.Balance}:{from}", initialFromBalance);
            State.SetUInt256($"{PoolStateKeys.Balance}:{to}", initialToBalance);
            State.SetUInt256($"{PoolStateKeys.Allowance}:{from}:{to}", initialSpenderAllowance);
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
            var pool = CreateNewOpdexStakingPool();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 150;
            UInt256 initialFromBalance = 100;
            UInt256 spenderAllowance = 150;

            State.SetUInt256($"{PoolStateKeys.Balance}:{from}", initialFromBalance);
            State.SetUInt256($"{PoolStateKeys.Allowance}:{from}:{to}", spenderAllowance);
            SetupMessage(Pool, to);

            pool.TransferFrom(from, to, amount).Should().BeFalse();
        }

        [Fact]
        public void TransferFrom_Fails_InsufficientSpenderAllowance()
        {
            var pool = CreateNewOpdexStakingPool();
            var from = Trader0;
            var to = Trader1;
            UInt256 amount = 200;
            UInt256 initialFromBalance = 1000;
            UInt256 spenderAllowance = 150;

            State.SetUInt256($"{PoolStateKeys.Balance}:{from}", initialFromBalance);
            State.SetUInt256($"{PoolStateKeys.Allowance}:{from}:{to}", spenderAllowance);
            SetupMessage(Pool, to);

            pool.TransferFrom(from, to, amount).Should().BeFalse();
        }

        [Fact]
        public void Approve_Success()
        {
            var pool = CreateNewOpdexStakingPool();
            var from = Trader0;
            var spender = Trader1;
            UInt256 amount = 100;
            UInt256 allowance = 10;

            SetupMessage(Pool, from);

            State.SetUInt256($"{PoolStateKeys.Allowance}:{from}:{spender}", allowance);

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

            State.SetUInt64(PoolStateKeys.ReserveCrs, currentReserveCrs);
            State.SetUInt256(PoolStateKeys.ReserveSrc, currentReserveSrc);
            State.SetUInt256(PoolStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256(PoolStateKeys.KLast, currentKLast);
            State.SetUInt256($"{PoolStateKeys.Balance}:{trader}", currentTraderBalance);

            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceSrc));

            var mintedLiquidity = pool.Mint(trader);
            mintedLiquidity.Should().Be(expectedLiquidity);

            pool.KLast.Should().Be(UInt256.Zero); // nodody staking
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
                To = trader,
                AmountLpt = expectedLiquidity,
                TotalSupply = expectedLiquidity + expectedBurnAmount
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
            const ulong currentBalanceCrs = 5_500;
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
            UInt256 expectedTotalSupply = currentTotalSupply + expectedLiquidity + mintedFee;

            var pool = CreateNewOpdexStakingPool(currentBalanceCrs);
            SetupMessage(Pool, trader);

            State.SetUInt64(PoolStateKeys.ReserveCrs, currentReserveCrs);
            State.SetUInt256(PoolStateKeys.ReserveSrc, currentReserveSrc);
            State.SetUInt256(PoolStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256(PoolStateKeys.KLast, expectedKLast);
            State.SetUInt256(PoolStateKeys.TotalStaked, 123);
            State.SetAddress(PoolStateKeys.StakingToken, StakingToken);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Trader0}", currentTraderBalance);

            var expectedSrcBalanceParams = new object[] {Pool};
            SetupCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), expectedSrcBalanceParams, TransferResult.Transferred(currentBalanceSrc));

            var mintedLiquidity = pool.Mint(Trader0);
            mintedLiquidity.Should().Be(expectedLiquidity);

            pool.KLast.Should().Be(expectedK);
            pool.TotalSupply.Should().Be(expectedTotalSupply);
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
                To = trader,
                AmountLpt = expectedLiquidity,
                TotalSupply = expectedTotalSupply
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

            State.SetUInt64(PoolStateKeys.ReserveCrs, currentReserveCrs);
            State.SetUInt256(PoolStateKeys.ReserveSrc, currentReserveSrc);
            State.SetUInt256(PoolStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256(PoolStateKeys.KLast, currentKLast);
            State.SetUInt256($"{PoolStateKeys.Balance}:{trader}", currentTraderBalance);

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

            SetupMessage(Pool, StakingMarket);

            State.SetBool(PoolStateKeys.Locked, true);

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
            UInt256 expectedTotalSupply = currentTotalSupply + expectedMintedFee - burnAmount;

            var pool = CreateNewOpdexStakingPool(currentReserveCrs);
            SetupMessage(Pool, StakingMarket);
            State.SetUInt64(PoolStateKeys.ReserveCrs, currentReserveCrs);
            State.SetUInt256(PoolStateKeys.ReserveSrc, currentReserveSrc);
            State.SetUInt256(PoolStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", burnAmount);
            State.SetUInt256(PoolStateKeys.KLast, currentKLast);
            State.SetUInt256(PoolStateKeys.TotalStaked, 123);
            State.SetAddress(PoolStateKeys.StakingToken, StakingToken);

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
            pool.TotalSupply.Should().Be(expectedTotalSupply);

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
                Sender = StakingMarket,
                To = to,
                AmountCrs = expectedReceivedCrs,
                AmountSrc = expectedReceivedSrc,
                AmountLpt = burnAmount,
                TotalSupply = expectedTotalSupply
            }, Times.Once);
        }

        [Fact]
        public void BurnAllLiquidity_Success()
        {
            const ulong currentReserveCrs = 100_000;
            UInt256 currentReserveSrc = 1_000_000;
            UInt256 currentTotalSupply = 15_000;
            UInt256 burnAmount = 14_000; // Total Supply - Minimum Liquidity
            const ulong expectedReceivedCrs = 93_333;
            UInt256 expectedReceivedSrc = 933_333;
            UInt256 expectedMintedFee = 0;
            var to = Trader0;

            var pool = CreateNewOpdexStakingPool(currentReserveCrs);
            SetupMessage(Pool, StakingMarket);
            State.SetUInt64(PoolStateKeys.ReserveCrs, currentReserveCrs);
            State.SetUInt256(PoolStateKeys.ReserveSrc, currentReserveSrc);
            State.SetUInt256(PoolStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", burnAmount);

            SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc));
            SetupTransfer(to, expectedReceivedCrs, TransferResult.Transferred(true));
            SetupCall(Token, 0, nameof(IOpdexStakingPool.TransferTo), new object[] { to, expectedReceivedSrc }, TransferResult.Transferred(true), () =>
            {
                SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(currentReserveSrc - expectedReceivedSrc));
            });

            var results = pool.Burn(to);
            results[0].Should().Be((UInt256)expectedReceivedCrs);
            results[1].Should().Be(expectedReceivedSrc);
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
                Sender = StakingMarket,
                To = to,
                AmountCrs = expectedReceivedCrs,
                AmountSrc = expectedReceivedSrc,
                AmountLpt = burnAmount,
                TotalSupply = currentTotalSupply + expectedMintedFee - burnAmount
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
            SetupMessage(Pool, StakingMarket);
            State.SetUInt64(PoolStateKeys.ReserveCrs, currentReserveCrs);
            State.SetUInt256(PoolStateKeys.ReserveSrc, currentReserveSrc);
            State.SetUInt256(PoolStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", burnAmount);
            State.SetUInt256(PoolStateKeys.KLast, currentKLast);

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

            SetupMessage(Pool, StakingMarket);

            State.SetBool(PoolStateKeys.Locked, true);

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
        public void Swap_Success(ulong swapAmountCrs, ulong currentReserveCrs, UInt256 currentReserveSrc, UInt256 expectedReceivedToken)
        {
            var to = Trader0;

            var pool = CreateNewOpdexStakingPool(currentReserveCrs);

            SetupMessage(Pool, StakingMarket, swapAmountCrs);

            State.SetUInt64(PoolStateKeys.ReserveCrs, currentReserveCrs);
            State.SetUInt256(PoolStateKeys.ReserveSrc, currentReserveSrc);

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
                Sender = StakingMarket,
                To = to
            }, Times.Once);
        }

        #endregion

        #region Start Staking Tests

        [Fact]
        public void Stake_NewStaker_FirstStaker_Success()
        {
            UInt256 stakeAmount = 1_000;

            var pool = CreateNewOpdexStakingPool();

            var transferFromParameters = new object[] { Trader0, Pool, stakeAmount };
            SetupCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferFrom), transferFromParameters, TransferResult.Transferred(true));

            SetupMessage(Pool, Trader0);

            pool.StartStaking(stakeAmount);

            pool.TotalStaked.Should().Be(stakeAmount);
            pool.GetStakedBalance(Trader0).Should().Be(stakeAmount);
            pool.GetRewardPerStakedToken().Should().Be(UInt256.Zero);
            pool.GetStakingRewards(Trader0).Should().Be(UInt256.Zero);
            pool.RewardPerStakedTokenLast.Should().Be(UInt256.Zero);
            pool.StakingRewardsBalance.Should().Be(UInt256.Zero);
            pool.ApplicableStakingRewards.Should().Be(UInt256.Zero);

            VerifyCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferFrom), transferFromParameters, Times.Once);
            VerifyCall(StakingToken, 0ul, NominateLiquidityPoolMethod, null, Times.Once);

            VerifyLog(new StartStakingLog
            {
                Staker = Trader0,
                Amount = stakeAmount,
                TotalStaked = stakeAmount,
                StakerBalance = stakeAmount
            }, Times.Once);
        }

        [Fact]
        // Pool would have 1 provider, starting with 10k SRC and 10k CRS. Swaps occur adjusting fees and klast.
        public void Stake_NewStaker_ExistingStakersInPool_Success()
        {
            UInt256 stakeAmount = 1_000;
            UInt256 kLast = 100_000_000;
            UInt256 reserveSrc = 8_500;
            const ulong reserveCrs = 12_000;
            UInt256 totalSupply = 9_000; // sqrt(kLast) - minLiquidity
            UInt256 totalStaked = 10_000;
            UInt256 expectedMintedStakingRewards = 14;
            UInt256 expectedTotalStaked = stakeAmount + totalStaked;

            var pool = CreateNewOpdexStakingPool();

            State.SetAddress(PoolStateKeys.StakingToken, StakingToken);
            State.SetUInt256(PoolStateKeys.ReserveSrc, reserveSrc);
            State.SetUInt64(PoolStateKeys.ReserveCrs, reserveCrs);
            State.SetUInt256(PoolStateKeys.KLast, kLast);
            State.SetUInt256(PoolStateKeys.TotalStaked, totalStaked);
            State.SetUInt256(PoolStateKeys.TotalSupply, totalSupply);
            State.SetUInt256($"{PoolStateKeys.StakedBalance}:{Trader0}", 0);

            var transferFromParameters = new object[] { Trader0, Pool, stakeAmount };
            SetupCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferFrom), transferFromParameters, TransferResult.Transferred(true));

            SetupMessage(Pool, Trader0);

            pool.StartStaking(stakeAmount);

            pool.TotalStaked.Should().Be(expectedTotalStaked);
            pool.StakingRewardsBalance.Should().Be(expectedMintedStakingRewards);
            pool.ApplicableStakingRewards.Should().Be(UInt256.Zero);
            pool.KLast.Should().Be(reserveCrs * reserveSrc);
            pool.GetStakedBalance(Trader0).Should().Be(stakeAmount);
            pool.GetStakingRewards(Trader0).Should().Be(UInt256.Zero);
            pool.GetStoredRewardPerStakedToken(Trader0).Should().NotBe(UInt256.Zero);
            pool.GetStoredReward(Trader0).Should().Be(UInt256.Zero);

            VerifyCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferFrom), transferFromParameters, Times.Once);
            VerifyCall(StakingToken, 0ul, NominateLiquidityPoolMethod, null, Times.Once);

            VerifyLog(new StartStakingLog
            {
                Staker = Trader0,
                Amount = stakeAmount,
                TotalStaked = expectedTotalStaked,
                StakerBalance = stakeAmount
            }, Times.Once);
        }

        [Fact]
        public void Stake_AddToTraderBalance_ExistingRewards_Success()
        {
            UInt256 stakingRewardsBalance = 1_000;
            UInt256 currentStakerBalance = 1_000;
            UInt256 stakeAmount = 500;
            UInt256 kLast = 100_000_000;
            UInt256 reserveSrc = 8_500;
            const ulong reserveCrs = 12_000;
            UInt256 totalSupply = 9_000; // sqrt(kLast) - minLiquidity
            UInt256 totalStaked = 10_000;
            UInt256 expectedMintedStakingRewards = 14;
            UInt256 expectedTotalStaked = stakeAmount + totalStaked;
            UInt256 expectedStakerRewards = (stakingRewardsBalance + expectedMintedStakingRewards) / (totalStaked / currentStakerBalance);

            var pool = CreateNewOpdexStakingPool();

            State.SetAddress(PoolStateKeys.StakingToken, StakingToken);
            State.SetUInt256(PoolStateKeys.ReserveSrc, reserveSrc);
            State.SetUInt64(PoolStateKeys.ReserveCrs, reserveCrs);
            State.SetUInt256(PoolStateKeys.KLast, kLast);
            State.SetUInt256(PoolStateKeys.TotalStaked, totalStaked);
            State.SetUInt256(PoolStateKeys.StakingRewardsBalance, stakingRewardsBalance);
            State.SetUInt256(PoolStateKeys.ApplicableStakingRewards, stakingRewardsBalance);
            State.SetUInt256(PoolStateKeys.TotalSupply, totalSupply);
            State.SetUInt256($"{PoolStateKeys.StakedBalance}:{Trader0}", currentStakerBalance);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", stakingRewardsBalance);

            var transferFromParameters = new object[] { Trader0, Pool, stakeAmount };
            SetupCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferFrom), transferFromParameters, TransferResult.Transferred(true));

            SetupMessage(Pool, Trader0);

            pool.StartStaking(stakeAmount);

            pool.TotalStaked.Should().Be(expectedTotalStaked);
            pool.GetStakedBalance(Trader0).Should().Be(stakeAmount + currentStakerBalance);
            pool.StakingRewardsBalance.Should().Be(stakingRewardsBalance + expectedMintedStakingRewards);
            pool.ApplicableStakingRewards.Should().Be(UInt256.Zero);
            pool.KLast.Should().Be(reserveCrs * reserveSrc);
            pool.GetStakingRewards(Trader0).Should().Be(expectedStakerRewards);
            pool.GetStoredRewardPerStakedToken(Trader0).Should().NotBe(UInt256.Zero);
            pool.GetStoredReward(Trader0).Should().Be(expectedStakerRewards);

            VerifyCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferFrom), transferFromParameters, Times.Once);
            VerifyCall(StakingToken, 0ul, NominateLiquidityPoolMethod, null, Times.Once);

            VerifyLog(new StartStakingLog
            {
                Staker = Trader0,
                Amount = stakeAmount,
                TotalStaked = expectedTotalStaked,
                StakerBalance = stakeAmount + currentStakerBalance
            }, Times.Once);
        }

        [Fact]
        public void Stake_Throws_Locked()
        {
            var pool = CreateNewOpdexStakingPool();

            State.SetBool(PoolStateKeys.Locked, true);

            pool
                .Invoking(p => p.StartStaking(UInt256.Zero))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }

        [Fact]
        public void Stake_Throws_StakingUnavailable()
        {
            var pool = CreateNewOpdexStakingPool();

            State.SetAddress(PoolStateKeys.StakingToken, Address.Zero);

            pool
                .Invoking(p => p.StartStaking(UInt256.Zero))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: STAKING_UNAVAILABLE");
        }

        [Fact]
        public void Stake_Throws_ZeroAmount()
        {
            var pool = CreateNewOpdexStakingPool();

            pool
                .Invoking(p => p.StartStaking(UInt256.Zero))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: CANNOT_STAKE_ZERO");
        }

        #endregion

        #region Collect Staking Rewards Tests

        [Fact]
        public void Collect_Burn_Success()
        {
            const ulong reserveCrs = 1_000;
            UInt256 reserveSrc = 1_000;
            UInt256 kLast = 900_000;
            UInt256 currentTotalSupply = 1_000;
            UInt256 totalStaked = 500;
            UInt256 stakingRewardsBalance = 200;
            UInt256 currentStakerBalance = 125;
            UInt256 rewardPerToken = stakingRewardsBalance * 100_000_000 / totalStaked;
            UInt256 expectedReward = 52;
            const ulong expectedRewardCrs = 51;
            UInt256 expectedRewardSrc = 51;
            UInt256 expectedMintedRewards = 8;
            UInt256 expectedKLast = (reserveCrs - expectedRewardCrs) * (reserveSrc - expectedRewardSrc);
            UInt256 expectedTotalSupply = currentTotalSupply + expectedMintedRewards - expectedReward;

            var pool = CreateNewOpdexStakingPool();

            State.SetAddress(PoolStateKeys.StakingToken, StakingToken);
            State.SetUInt256(PoolStateKeys.ReserveSrc, reserveSrc);
            State.SetUInt64(PoolStateKeys.ReserveCrs, reserveCrs);
            State.SetUInt256(PoolStateKeys.KLast, kLast);
            State.SetUInt256(PoolStateKeys.TotalStaked, totalStaked);
            State.SetUInt256(PoolStateKeys.StakingRewardsBalance, stakingRewardsBalance);
            State.SetUInt256(PoolStateKeys.RewardPerStakedTokenLast, rewardPerToken);
            State.SetUInt256(PoolStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256($"{PoolStateKeys.StakedBalance}:{Trader0}", currentStakerBalance);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", stakingRewardsBalance);

            SetupMessage(Pool, Trader0);
            SetupBalance(reserveCrs);

            // Initial get balance call
            SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(reserveSrc), () =>
            {
                // second get balance call - after withdrawal from reserves
                SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(reserveSrc - expectedRewardSrc));
            });

            SetupTransfer(Trader0, expectedRewardCrs, TransferResult.Transferred(true));
            SetupCall(Token, 0, nameof(IOpdexStakingPool.TransferTo), new object[] { Trader0, expectedRewardSrc }, TransferResult.Transferred(true));

            pool.CollectStakingRewards(true);

            pool.TotalStaked.Should().Be(totalStaked);
            pool.TotalSupply.Should().Be(expectedTotalSupply);
            pool.StakingRewardsBalance.Should().Be(stakingRewardsBalance + expectedMintedRewards - expectedReward);
            pool.ApplicableStakingRewards.Should().Be(UInt256.Zero);
            pool.ReserveSrc.Should().Be(reserveSrc - expectedRewardSrc);
            pool.ReserveCrs.Should().Be(reserveCrs - expectedRewardCrs);
            pool.KLast.Should().Be(expectedKLast);
            pool.GetStakedBalance(Trader0).Should().Be(currentStakerBalance);
            pool.GetBalance(Trader0).Should().Be(UInt256.Zero);
            pool.GetStoredReward(Trader0).Should().Be(UInt256.Zero);
            pool.GetStoredRewardPerStakedToken(Trader0).Should().NotBe(UInt256.Zero);

            VerifyCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, Times.Exactly(2));
            VerifyTransfer(Trader0, expectedRewardCrs, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStakingPool.TransferTo), new object[] { Trader0, expectedRewardSrc }, Times.Once);

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
                AmountSrc = expectedRewardSrc,
                AmountLpt = expectedReward,
                TotalSupply = expectedTotalSupply
            }, Times.Once);

            VerifyLog(new CollectStakingRewardsLog
            {
                Staker = Trader0,
                Amount = expectedReward
            }, Times.Once);
        }

        [Fact]
        public void Collect_DontBurn_Success()
        {
            const ulong reserveCrs = 1_000;
            UInt256 reserveSrc = 1_000;
            UInt256 kLast = 900_000;
            UInt256 currentTotalSupply = 1_000;
            UInt256 totalStaked = 500;
            UInt256 stakingRewardsBalance = 200;
            UInt256 currentStakerBalance = 125;
            UInt256 rewardPerToken = stakingRewardsBalance * 100_000_000 / totalStaked;
            UInt256 expectedReward = 52;
            UInt256 expectedMintedRewards = 8;

            var pool = CreateNewOpdexStakingPool();

            State.SetAddress(PoolStateKeys.StakingToken, StakingToken);
            State.SetUInt256(PoolStateKeys.ReserveSrc, reserveSrc);
            State.SetUInt64(PoolStateKeys.ReserveCrs, reserveCrs);
            State.SetUInt256(PoolStateKeys.KLast, kLast);
            State.SetUInt256(PoolStateKeys.TotalStaked, totalStaked);
            State.SetUInt256(PoolStateKeys.StakingRewardsBalance, stakingRewardsBalance);
            State.SetUInt256(PoolStateKeys.RewardPerStakedTokenLast, rewardPerToken);
            State.SetUInt256(PoolStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256($"{PoolStateKeys.StakedBalance}:{Trader0}", currentStakerBalance);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", stakingRewardsBalance);

            SetupMessage(Pool, Trader0);

            pool.CollectStakingRewards(false);

            pool.TotalStaked.Should().Be(totalStaked);
            pool.TotalSupply.Should().Be(currentTotalSupply + expectedMintedRewards);
            pool.StakingRewardsBalance.Should().Be(stakingRewardsBalance + expectedMintedRewards - expectedReward);
            pool.ApplicableStakingRewards.Should().Be(UInt256.Zero);
            pool.ReserveSrc.Should().Be(reserveSrc);
            pool.ReserveCrs.Should().Be(reserveCrs);
            pool.KLast.Should().Be(reserveCrs * reserveSrc);
            pool.GetStakedBalance(Trader0).Should().Be(currentStakerBalance);
            pool.GetBalance(Trader0).Should().Be(expectedReward);
            pool.GetStoredReward(Trader0).Should().Be(UInt256.Zero);
            pool.GetStoredRewardPerStakedToken(Trader0).Should().NotBe(UInt256.Zero);

            VerifyLog(new CollectStakingRewardsLog
            {
                Staker = Trader0,
                Amount = expectedReward
            }, Times.Once);
        }

        [Fact]
        public void Collect_NoRewards_Success()
        {
            const ulong reserveCrs = 2_500;
            UInt256 reserveSrc = 1_000;
            UInt256 kLast = reserveCrs * reserveSrc; // Intentionally unchanged
            UInt256 totalSupply = 1_000;
            UInt256 totalStaked = 500;
            UInt256 stakingRewardsBalance = 0;
            UInt256 currentStakerBalance = 250;
            UInt256 expectedReward = 0;

            var pool = CreateNewOpdexStakingPool();

            State.SetAddress(PoolStateKeys.StakingToken, StakingToken);
            State.SetUInt256(PoolStateKeys.ReserveSrc, reserveSrc);
            State.SetUInt64(PoolStateKeys.ReserveCrs, reserveCrs);
            State.SetUInt256(PoolStateKeys.KLast, kLast);
            State.SetUInt256(PoolStateKeys.TotalStaked, totalStaked);
            State.SetUInt256(PoolStateKeys.StakingRewardsBalance, stakingRewardsBalance);
            State.SetUInt256(PoolStateKeys.TotalSupply, totalSupply);
            State.SetUInt256($"{PoolStateKeys.StakedBalance}:{Trader0}", currentStakerBalance);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", stakingRewardsBalance);

            SetupMessage(Pool, Trader0);

            pool.CollectStakingRewards(false);

            pool.TotalStaked.Should().Be(totalStaked);
            pool.TotalSupply.Should().Be(totalSupply);
            pool.KLast.Should().Be(kLast);
            pool.ReserveCrs.Should().Be(reserveCrs);
            pool.ReserveSrc.Should().Be(reserveSrc);
            pool.ApplicableStakingRewards.Should().Be(UInt256.Zero);
            pool.GetStakedBalance(Trader0).Should().Be(currentStakerBalance);
            pool.GetBalance(Trader0).Should().Be(expectedReward);
            pool.GetStoredReward(Trader0).Should().Be(UInt256.Zero);
            pool.GetStoredRewardPerStakedToken(Trader0).Should().Be(UInt256.Zero);

            VerifyLog(It.IsAny<CollectStakingRewardsLog>(), Times.Never);
        }

        [Fact]
        public void Collect_Throws_Locked()
        {
            var pool = CreateNewOpdexStakingPool();

            State.SetBool(PoolStateKeys.Locked, true);

            pool
                .Invoking(p => p.CollectStakingRewards(false))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }

        [Fact]
        public void Collect_Throws_StakingUnavailable()
        {
            var pool = CreateNewOpdexStakingPool();

            State.SetAddress(PoolStateKeys.StakingToken, Address.Zero);

            pool
                .Invoking(p => p.CollectStakingRewards(false))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: STAKING_UNAVAILABLE");
        }

        #endregion

        #region Stop Staking Tests

        [Fact]
        public void StopStaking_AndBurn_Success()
        {
            const ulong reserveCrs = 1_000;
            UInt256 reserveSrc = 1_000;
            UInt256 kLast = 900_000;
            UInt256 currentTotalSupply = 1_000;
            UInt256 totalStaked = 500;
            UInt256 stakingRewardsBalance = 200;
            UInt256 currentStakerBalance = 125;
            UInt256 rewardPerToken = stakingRewardsBalance * 100_000_000 / totalStaked;
            UInt256 expectedReward = 52;
            const ulong expectedRewardCrs = 51;
            UInt256 expectedRewardSrc = 51;
            UInt256 expectedMintedRewards = 8;
            UInt256 expectedTotalStaked = totalStaked - currentStakerBalance;
            UInt256 expectedKLast = (reserveCrs - expectedRewardCrs) * (reserveSrc - expectedRewardSrc);
            UInt256 expectedTotalSupply = currentTotalSupply - expectedReward + expectedMintedRewards;

            var pool = CreateNewOpdexStakingPool();

            State.SetAddress(PoolStateKeys.StakingToken, StakingToken);
            State.SetUInt256(PoolStateKeys.ReserveSrc, reserveSrc);
            State.SetUInt64(PoolStateKeys.ReserveCrs, reserveCrs);
            State.SetUInt256(PoolStateKeys.KLast, kLast);
            State.SetUInt256(PoolStateKeys.TotalStaked, totalStaked);
            State.SetUInt256(PoolStateKeys.StakingRewardsBalance, stakingRewardsBalance);
            State.SetUInt256(PoolStateKeys.RewardPerStakedTokenLast, rewardPerToken);
            State.SetUInt256(PoolStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256($"{PoolStateKeys.StakedBalance}:{Trader0}", currentStakerBalance);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", stakingRewardsBalance);

            SetupMessage(Pool, Trader0);
            SetupBalance(reserveCrs);

            var transferToTakingTokenParams = new object[] {Trader0, currentStakerBalance};
            SetupCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferTo), transferToTakingTokenParams, TransferResult.Transferred(true));

            var getBalanceParams = new object[] {Pool};
            // Initial get balance call
            SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), getBalanceParams, TransferResult.Transferred(reserveSrc), () =>
            {
                // second get balance call
                SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), getBalanceParams, TransferResult.Transferred(reserveSrc - expectedRewardSrc));
            });

            SetupTransfer(Trader0, expectedRewardCrs, TransferResult.Transferred(true));

            var transferToReserveSrcParams = new object[] {Trader0, expectedRewardSrc};
            SetupCall(Token, 0, nameof(IOpdexStakingPool.TransferTo), transferToReserveSrcParams, TransferResult.Transferred(true));

            pool.StopStaking(currentStakerBalance, true);

            pool.TotalStaked.Should().Be(expectedTotalStaked);
            pool.TotalSupply.Should().Be(expectedTotalSupply);
            pool.StakingRewardsBalance.Should().Be(stakingRewardsBalance + expectedMintedRewards - expectedReward);
            pool.ApplicableStakingRewards.Should().Be(UInt256.Zero);
            pool.ReserveSrc.Should().Be(reserveSrc - expectedRewardSrc);
            pool.ReserveCrs.Should().Be(reserveCrs - expectedRewardCrs);
            pool.KLast.Should().Be(expectedKLast);
            pool.GetStakedBalance(Trader0).Should().Be(UInt256.Zero);
            pool.GetBalance(Trader0).Should().Be(UInt256.Zero);
            pool.GetStoredReward(Trader0).Should().Be(UInt256.Zero);
            pool.GetStoredRewardPerStakedToken(Trader0).Should().NotBe(UInt256.Zero);

            VerifyCall(Token, 0ul, nameof(IOpdexStakingPool.GetBalance), getBalanceParams, Times.Exactly(2));
            VerifyCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferTo), transferToTakingTokenParams, Times.Once);
            VerifyCall(StakingToken, 0ul, NominateLiquidityPoolMethod, null, Times.Once);
            VerifyTransfer(Trader0, expectedRewardCrs, Times.Once);
            VerifyCall(Token, 0ul, nameof(IOpdexStakingPool.TransferTo), transferToReserveSrcParams, Times.Once);

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
                AmountSrc = expectedRewardSrc,
                AmountLpt = expectedReward,
                TotalSupply = expectedTotalSupply
            }, Times.Once);

            VerifyLog(new StopStakingLog
            {
                Staker = Trader0,
                Amount = currentStakerBalance,
                TotalStaked = expectedTotalStaked,
                StakerBalance = UInt256.Zero
            }, Times.Once);

            VerifyLog(new CollectStakingRewardsLog
            {
                Staker = Trader0,
                Amount = expectedReward
            }, Times.Once);
        }

        [Fact]
        public void StopStaking_DontBurn_Success()
        {
            const ulong reserveCrs = 1_000;
            UInt256 reserveSrc = 1_000;
            UInt256 kLast = 900_000;
            UInt256 currentTotalSupply = 1_000;
            UInt256 totalStaked = 500;
            UInt256 stakingRewardsBalance = 200;
            UInt256 currentStakerBalance = 125;
            UInt256 rewardPerToken = stakingRewardsBalance * 100_000_000 / totalStaked;
            UInt256 expectedReward = 52;
            UInt256 expectedMintedRewards = 8;
            UInt256 expectedTotalStaked = totalStaked - currentStakerBalance;

            var pool = CreateNewOpdexStakingPool();

            State.SetAddress(PoolStateKeys.StakingToken, StakingToken);
            State.SetUInt256(PoolStateKeys.ReserveSrc, reserveSrc);
            State.SetUInt64(PoolStateKeys.ReserveCrs, reserveCrs);
            State.SetUInt256(PoolStateKeys.KLast, kLast);
            State.SetUInt256(PoolStateKeys.TotalStaked, totalStaked);
            State.SetUInt256(PoolStateKeys.StakingRewardsBalance, stakingRewardsBalance);
            State.SetUInt256(PoolStateKeys.RewardPerStakedTokenLast, rewardPerToken);
            State.SetUInt256(PoolStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256($"{PoolStateKeys.StakedBalance}:{Trader0}", currentStakerBalance);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", stakingRewardsBalance);

            SetupMessage(Pool, Trader0);

            var transferToParams = new object[] {Trader0, currentStakerBalance};
            SetupCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferTo), transferToParams, TransferResult.Transferred(true));

            pool.StopStaking(currentStakerBalance, false);

            pool.TotalStaked.Should().Be(expectedTotalStaked);
            pool.TotalSupply.Should().Be(currentTotalSupply + expectedMintedRewards);
            pool.StakingRewardsBalance.Should().Be(stakingRewardsBalance + expectedMintedRewards - expectedReward);
            pool.ApplicableStakingRewards.Should().Be(UInt256.Zero);
            pool.ReserveSrc.Should().Be(reserveSrc);
            pool.ReserveCrs.Should().Be(reserveCrs);
            pool.KLast.Should().Be(reserveSrc * reserveCrs);
            pool.GetStakedBalance(Trader0).Should().Be(UInt256.Zero);
            pool.GetBalance(Trader0).Should().Be(expectedReward);
            pool.GetStoredReward(Trader0).Should().Be(UInt256.Zero);
            pool.GetStoredRewardPerStakedToken(Trader0).Should().NotBe(UInt256.Zero);

            VerifyCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferTo), transferToParams, Times.Once);
            VerifyCall(StakingToken, 0ul, NominateLiquidityPoolMethod, null, Times.Once);

            VerifyLog(new TransferLog
            {
                From = Pool,
                Amount = expectedReward,
                To = Trader0
            }, Times.Once);

            VerifyLog(new CollectStakingRewardsLog
            {
                Staker = Trader0,
                Amount = expectedReward
            }, Times.Once);

            VerifyLog(new StopStakingLog
            {
                Staker = Trader0,
                Amount = currentStakerBalance,
                TotalStaked = expectedTotalStaked,
                StakerBalance = UInt256.Zero
            }, Times.Once);
        }

        [Fact]
        public void StopStaking_PartialAmount_Success()
        {
            const ulong reserveCrs = 1_000;
            UInt256 reserveSrc = 1_000;
            UInt256 kLast = 900_000;
            UInt256 currentTotalSupply = 1_000;
            UInt256 totalStaked = 500;
            UInt256 stakingRewardsBalance = 200;
            UInt256 currentStakerBalance = 125;
            UInt256 stopStakingAmount = 50;
            UInt256 rewardPerToken = stakingRewardsBalance * 100_000_000 / totalStaked;
            UInt256 expectedReward = 52;
            UInt256 expectedMintedRewards = 8;
            UInt256 expectedTotalStaked = totalStaked - stopStakingAmount;

            var pool = CreateNewOpdexStakingPool();

            State.SetAddress(PoolStateKeys.StakingToken, StakingToken);
            State.SetUInt256(PoolStateKeys.ReserveSrc, reserveSrc);
            State.SetUInt64(PoolStateKeys.ReserveCrs, reserveCrs);
            State.SetUInt256(PoolStateKeys.KLast, kLast);
            State.SetUInt256(PoolStateKeys.TotalStaked, totalStaked);
            State.SetUInt256(PoolStateKeys.StakingRewardsBalance, stakingRewardsBalance);
            State.SetUInt256(PoolStateKeys.RewardPerStakedTokenLast, rewardPerToken);
            State.SetUInt256(PoolStateKeys.TotalSupply, currentTotalSupply);
            State.SetUInt256($"{PoolStateKeys.StakedBalance}:{Trader0}", currentStakerBalance);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", stakingRewardsBalance);

            SetupMessage(Pool, Trader0);

            var transferToParams = new object[] {Trader0, stopStakingAmount};
            SetupCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferTo), transferToParams, TransferResult.Transferred(true));

            pool.StopStaking(stopStakingAmount, false);

            pool.TotalStaked.Should().Be(expectedTotalStaked);
            pool.TotalSupply.Should().Be(currentTotalSupply + expectedMintedRewards);
            pool.StakingRewardsBalance.Should().Be(stakingRewardsBalance + expectedMintedRewards - expectedReward);
            pool.ApplicableStakingRewards.Should().Be(UInt256.Zero);
            pool.ReserveSrc.Should().Be(reserveSrc);
            pool.ReserveCrs.Should().Be(reserveCrs);
            pool.KLast.Should().Be(reserveSrc * reserveCrs);
            pool.GetStakedBalance(Trader0).Should().Be(currentStakerBalance - stopStakingAmount);
            pool.GetBalance(Trader0).Should().Be(expectedReward);
            pool.GetStoredReward(Trader0).Should().Be(UInt256.Zero);
            pool.GetStoredRewardPerStakedToken(Trader0).Should().NotBe(UInt256.Zero);

            VerifyCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferTo), transferToParams, Times.Once);
            VerifyCall(StakingToken, 0ul, NominateLiquidityPoolMethod, null, Times.Once);

            VerifyLog(new TransferLog
            {
                From = Pool,
                Amount = expectedReward,
                To = Trader0
            }, Times.Once);

            VerifyLog(new CollectStakingRewardsLog
            {
                Staker = Trader0,
                Amount = expectedReward
            }, Times.Once);

            VerifyLog(new StopStakingLog
            {
                Staker = Trader0,
                Amount = stopStakingAmount,
                TotalStaked = expectedTotalStaked,
                StakerBalance = currentStakerBalance - stopStakingAmount
            }, Times.Once);
        }

        [Fact]
        public void StopStaking_NoRewards_Success()
        {
            const ulong reserveCrs = 2_343_485;
            UInt256 reserveSrc = 23_532_234_235;
            UInt256 kLast = reserveCrs * reserveSrc; // intentionally no difference
            UInt256 totalSupply = 1_000_000_000;
            UInt256 totalStaked = 10_000;
            UInt256 stakingRewardsBalance = 0;
            UInt256 currentStakerBalance = 10_000;
            UInt256 expectedReward = 0;
            UInt256 expectedTotalStaked = 0;
            UInt256 expectedStakerBalance = 0;

            var pool = CreateNewOpdexStakingPool();

            State.SetAddress(PoolStateKeys.StakingToken, StakingToken);
            State.SetUInt256(PoolStateKeys.ReserveSrc, reserveSrc);
            State.SetUInt64(PoolStateKeys.ReserveCrs, reserveCrs);
            State.SetUInt256(PoolStateKeys.KLast, kLast);
            State.SetUInt256(PoolStateKeys.TotalStaked, totalStaked);
            State.SetUInt256(PoolStateKeys.StakingRewardsBalance, stakingRewardsBalance);
            State.SetUInt256(PoolStateKeys.TotalSupply, totalSupply);
            State.SetUInt256($"{PoolStateKeys.StakedBalance}:{Trader0}", currentStakerBalance);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", stakingRewardsBalance);

            SetupMessage(Pool, Trader0);

            var transferToParams = new object[] {Trader0, currentStakerBalance};
            SetupCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferTo), transferToParams, TransferResult.Transferred(true));

            pool.StopStaking(currentStakerBalance, false);

            pool.TotalStaked.Should().Be(expectedTotalStaked);

            pool.TotalStaked.Should().Be(expectedTotalStaked);
            pool.TotalSupply.Should().Be(totalSupply);
            pool.StakingRewardsBalance.Should().Be(UInt256.Zero);
            pool.ApplicableStakingRewards.Should().Be(UInt256.Zero);
            pool.ReserveSrc.Should().Be(reserveSrc);
            pool.ReserveCrs.Should().Be(reserveCrs);
            pool.KLast.Should().Be(UInt256.Zero); // No stakers
            pool.GetStakedBalance(Trader0).Should().Be(expectedStakerBalance);
            pool.GetBalance(Trader0).Should().Be(expectedReward);
            pool.GetStoredReward(Trader0).Should().Be(UInt256.Zero);
            pool.GetStoredRewardPerStakedToken(Trader0).Should().Be(UInt256.Zero);

            VerifyCall(StakingToken, 0ul, nameof(IOpdexStakingPool.TransferTo), transferToParams, Times.Once);
            VerifyCall(StakingToken, 0ul, NominateLiquidityPoolMethod, null, Times.Once);

            VerifyLog(It.IsAny<CollectStakingRewardsLog>(), Times.Never);

            VerifyLog(new StopStakingLog
            {
                Staker = Trader0,
                Amount = currentStakerBalance,
                TotalStaked = expectedTotalStaked,
                StakerBalance = expectedStakerBalance
            }, Times.Once);
        }

        [Fact]
        public void StopStaking_Throws_Locked()
        {
            var pool = CreateNewOpdexStakingPool();

            State.SetBool(PoolStateKeys.Locked, true);

            pool
                .Invoking(p => p.StopStaking(UInt256.MaxValue, false))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }

        [Fact]
        public void StopStaking_Throws_StakingUnavailable()
        {
            var pool = CreateNewOpdexStakingPool();

            State.SetAddress(PoolStateKeys.StakingToken, Address.Zero);

            pool
                .Invoking(p => p.StopStaking(UInt256.MaxValue, false))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: STAKING_UNAVAILABLE");
        }

        [Fact]
        public void StopStaking_Throws_InvalidAmount()
        {
            var pool = CreateNewOpdexStakingPool();

            pool
                .Invoking(p => p.StopStaking(UInt256.Zero, false))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_AMOUNT");
        }

        #endregion

        #region Staking Rewards

        [Theory]
        [InlineData(25, 25, 100, 100, 100)]
        [InlineData(25, 50, 50, 100, 25)]
        [InlineData(25, 100, 100, 3_000, 25)]
        public void GetStakingRewards_Success(UInt256 stakerBalance, UInt256 totalStaked, UInt256 newRewards, UInt256 totalRewards, UInt256 expectedRewards)
        {
            var staker = Trader0;

            var pool = CreateNewOpdexStakingPool();

            State.SetUInt256($"{PoolStateKeys.StakedBalance}:{staker}", stakerBalance);
            State.SetUInt256(PoolStateKeys.StakingRewardsBalance, totalRewards);
            State.SetUInt256(PoolStateKeys.ApplicableStakingRewards, newRewards);
            State.SetUInt256(PoolStateKeys.TotalStaked, totalStaked);

            var stakingRewards = pool.GetStakingRewards(staker);

            stakingRewards.Should().Be(expectedRewards);
        }

        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(0, 1, 0, 1)]
        [InlineData(100, 25, 50, 50_000_025)]
        public void GetRewardPerStakedToken_Success(UInt256 totalStaked, UInt256 rewardPerStakedTokenLast, UInt256 applicableStakingRewards, UInt256 expected)
        {
            var pool = CreateNewOpdexStakingPool();

            State.SetUInt256(PoolStateKeys.TotalStaked, totalStaked);
            State.SetUInt256(PoolStateKeys.RewardPerStakedTokenLast, rewardPerStakedTokenLast);
            State.SetUInt256(PoolStateKeys.ApplicableStakingRewards, applicableStakingRewards);

            pool.GetRewardPerStakedToken().Should().Be(expected);
        }

        #endregion

        #region Maintain State Staking Tests

        [Fact]
        public void SingleStaker_StartAtZeroWeight_Success()
        {
            UInt256 totalRewards = 0;
            UInt256 stakingAmount = 100;

            var pool = CreateNewOpdexStakingPool();

            StartStaking(pool, Trader0, stakingAmount);

            // 100 rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 100);

            StopStaking(pool, Trader0, stakingAmount, totalRewards);
        }

        [Fact]
        public void TwoStakers_SameStakingLength_Success()
        {
            UInt256 totalRewards = 0;
            UInt256 trader0Amount = 100;
            UInt256 trader1Amount = 100;

            var pool = CreateNewOpdexStakingPool();

            // staker 1 start
            StartStaking(pool, Trader0, trader0Amount);

            // staker 2 start
            StartStaking(pool, Trader1, trader1Amount);

            // rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 100);

            // staker 1 stop
            StopStaking(pool, Trader0, trader0Amount, totalRewards / 2);

            // staker 2 stop
            StopStaking(pool, Trader1, trader1Amount, totalRewards / 2);
        }

        [Fact]
        public void TwoStakers_PartialStakingTokensWithdraw_Success()
        {
            UInt256 totalRewards = 0;
            UInt256 trader0Amount = 100;
            UInt256 trader1Amount = 100;

            var pool = CreateNewOpdexStakingPool();

            // staker 1 start
            StartStaking(pool, Trader0, trader0Amount);

            // staker 2 start
            StartStaking(pool, Trader1, trader1Amount);

            // rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 100);

            var initialRewards = totalRewards / 2;

            // staker 1 stops staking 50 of their 100
            var stopAmount = trader0Amount - 50;
            StopStaking(pool, Trader0, stopAmount, initialRewards);

            // staker 2 collects
            CollectStakingReward(pool, Trader1, initialRewards);

            // +100 rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 100);

            // staker 2 stops staking their 100 tokens 2/3 total
            StopStaking(pool, Trader1, trader1Amount, 66);

            // staker 1 stops staking their 50 tokens 1/3 total
            StopStaking(pool, Trader0, stopAmount, 33);
        }

        [Fact]
        public void TwoStakers_DifferentStakingLength_Success()
        {
            UInt256 totalRewards = 0;
            UInt256 trader0Amount = 100;
            UInt256 trader1Amount = 100;

            var pool = CreateNewOpdexStakingPool();

            // staker 1 start
            StartStaking(pool, Trader0, trader0Amount);

            // rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 100);

            // staker 2 start
            StartStaking(pool, Trader1, trader1Amount);

            // rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 100);

            // staker 1 stop
            StopStaking(pool, Trader0, trader0Amount, 150);

            // staker 2 stop
            StopStaking(pool, Trader1, trader1Amount, 50);
        }

        [Fact]
        public void TwoStakers_DifferentStakingLength_CollectingRewards_Success()
        {
            UInt256 totalRewards = 0;
            UInt256 trader0Amount = 100;
            UInt256 trader1Amount = 100;

            var pool = CreateNewOpdexStakingPool();

            // staker 1 start
            StartStaking(pool, Trader0, trader0Amount);

            // 100 rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 100);

            // staker 2 start
            StartStaking(pool, Trader1, trader1Amount);

            // 100 rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 100);

            // staker 1 Collect
            CollectStakingReward(pool, Trader0, 150);

            // 100 rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 100);

            // staker 2 stop
            StopStaking(pool, Trader1, trader1Amount, 100);

            // staker 1 collect
            CollectStakingReward(pool, Trader0, 50);

            // 25 rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 25);

            // staker 1 stop
            StopStaking(pool, Trader0, trader0Amount, 25);
        }

        [Fact]
        public void TwoStakers_OneLargeAmount_WithSyncedDust_Success()
        {
            UInt256 totalRewards = 0;
            UInt256 trader0Amount = 1_000_000_000_000;
            UInt256 trader1Amount = 1_000;

            var pool = CreateNewOpdexStakingPool();

            // staker 1 start
            StartStaking(pool, Trader0, trader0Amount);

            // staker 2 start
            StartStaking(pool, Trader1, trader1Amount);

            // 100 rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 100_000_000);

            // 100 rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 900_000_000);

            // staker 2 stop
            StopStaking(pool, Trader1, trader1Amount, 0);

            // staker 1 stop (rounding differences)
            StopStaking(pool, Trader0, trader0Amount, 999_990_000);

            // sync pool - return rounded dust back in as ApplicableStakingRewards with nobody staking (no affect with 0 staking weight)
            SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(UInt256.Zero));
            pool.Sync();

            // start staking again
            StartStaking(pool, Trader0, trader1Amount);

            // collect 0 rewards
            CollectStakingReward(pool, Trader0, 0);

            // sync again - with active stakers, adds dust or donations from AddressBalance-StakingRewardsBalance difference to the latest fee for stakers
            SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(UInt256.Zero));
            pool.Sync();

            // Stop staking collecting dust
            StopStaking(pool, Trader0, trader1Amount, 10_000);
        }

        [Fact]
        public void TwoStakers_OneLargeAmount_WithSkimmedDust_Success()
        {
            UInt256 totalRewards = 0;
            UInt256 trader0Amount = 1_000_000_000_000;
            UInt256 trader1Amount = 1_000;

            var pool = CreateNewOpdexStakingPool();

            // staker 1 start
            StartStaking(pool, Trader0, trader0Amount);

            // staker 2 start
            StartStaking(pool, Trader1, trader1Amount);

            // 100 rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 100_000_000);

            // 100 rewards come in
            totalRewards = MintNewRewards(pool, totalRewards, 900_000_000);

            // staker 2 stop
            StopStaking(pool, Trader1, trader1Amount, 0);

            // staker 1 stop (rounding differences)
            StopStaking(pool, Trader0, trader0Amount, 999_990_000);

            // skim pool - return rounded dust
            SetupCall(Token, 0, nameof(IOpdexStakingPool.GetBalance), new object[] {Pool}, TransferResult.Transferred(UInt256.Zero));
            pool.Skim(Trader1);
            pool.GetBalance(Trader1).Should().Be((UInt256) 10_000);
        }

        private void StartStaking(IOpdexStakingPool pool, Address staker, UInt256 amount)
        {
            SetupMessage(Pool, staker);
            SetupCall(StakingToken, 0, nameof(IOpdexStakingPool.TransferFrom), new object[] { staker, Pool, amount}, TransferResult.Transferred(true));

            var currentTotalStaked = pool.TotalStaked;
            var totalStaked = currentTotalStaked + amount;

            pool.StartStaking(amount);
            pool.GetStakedBalance(staker).Should().Be(amount);
            pool.TotalStaked.Should().Be(totalStaked);
            pool.ApplicableStakingRewards.Should().Be(UInt256.Zero);

            VerifyCall(StakingToken, 0, nameof(IOpdexStakingPool.TransferFrom), new object[] { staker, Pool, amount}, Times.AtLeastOnce);
            VerifyLog(new StartStakingLog {Amount = amount, Staker = staker, TotalStaked = totalStaked, StakerBalance = amount}, Times.AtLeastOnce);
        }

        private void CollectStakingReward(IOpdexStakingPool pool, Address staker, UInt256 rewards)
        {
            SetupMessage(Pool, staker);

            var totalStakedRewards = pool.StakingRewardsBalance - rewards;

            pool.CollectStakingRewards(false);
            pool.GetStoredReward(staker).Should().Be(UInt256.Zero);
            pool.StakingRewardsBalance.Should().Be(pool.TotalStaked == 0 ? UInt256.Zero : totalStakedRewards);
            pool.ApplicableStakingRewards.Should().Be(UInt256.Zero);

            if (rewards > 0)
            {
                VerifyLog(new CollectStakingRewardsLog {Amount = rewards, Staker = staker}, Times.AtLeastOnce);
            }
        }

        private void StopStaking(IOpdexStakingPool pool, Address staker, UInt256 amount, UInt256 rewards)
        {
            SetupMessage(Pool, staker);
            SetupCall(StakingToken, 0, nameof(IOpdexStakingPool.TransferTo), new object[] { staker, amount}, TransferResult.Transferred(true));

            var currentStakedAmount = pool.GetStakedBalance(staker);
            var expectedStakedAmount = currentStakedAmount - amount;
            var totalStakingRewards = pool.StakingRewardsBalance - rewards;
            var totalStaked = pool.TotalStaked - amount;

            pool.StopStaking(amount, false);
            pool.GetStakedBalance(staker).Should().Be(expectedStakedAmount);
            pool.StakingRewardsBalance.Should().Be(totalStaked == 0 ? UInt256.Zero : totalStakingRewards);
            pool.TotalStaked.Should().Be(totalStaked);
            pool.ApplicableStakingRewards.Should().Be(UInt256.Zero);

            VerifyCall(StakingToken, 0, nameof(IOpdexStakingPool.TransferTo), new object[] { staker, amount}, Times.AtLeastOnce);

            if (rewards > 0)
            {
                VerifyLog(new CollectStakingRewardsLog {Amount = rewards, Staker = staker}, Times.AtLeastOnce);
            }

            VerifyLog(new StopStakingLog {Amount = amount, Staker = staker, TotalStaked = totalStaked, StakerBalance = expectedStakedAmount}, Times.AtLeastOnce);
        }

        private UInt256 MintNewRewards(IOpdexStakingPool pool, UInt256 currentTotalRewards, UInt256 newRewards)
        {
            var latestFeesApplicable = pool.ApplicableStakingRewards + newRewards;
            var totalRewards = currentTotalRewards + newRewards;

            State.SetUInt256(PoolStateKeys.StakingRewardsBalance, totalRewards);
            State.SetUInt256(PoolStateKeys.ApplicableStakingRewards, latestFeesApplicable);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Pool}", totalRewards);

            return totalRewards;
        }

        #endregion
    }
}
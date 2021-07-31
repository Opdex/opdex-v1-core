using FluentAssertions;
using Moq;
using OpdexV1Core.Tests.Base;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Standards;
using Xunit;

namespace OpdexV1Core.Tests.Pools
{
    public class OpdexMiningPoolTests : TestBase
    {
        [Fact]
        public void CreatesContract_Success()
        {
            var miningPool = CreateNewMiningPool();

            miningPool.MiningGovernance.Should().Be(MiningGovernance);
            miningPool.MinedToken.Should().Be(StakingToken);
            miningPool.StakingToken.Should().Be(Pool1);
            miningPool.MiningDuration.Should().Be(BlocksPerMonth);
            miningPool.TotalSupply.Should().Be(UInt256.Zero);
            miningPool.MiningPeriodEndBlock.Should().Be(0);
            miningPool.LastUpdateBlock.Should().Be(0);
            miningPool.RewardPerStakedTokenLast.Should().Be(UInt256.Zero);
        }

        [Fact]
        public void CreateContract_Throws_InvalidMiningGovernance()
        {
            SetupBalance(0);
            SetupBlock(10);

            SetupCall(StakingToken, 0, "get_MiningGovernance", null, TransferResult.Failed());

            this.Invoking(p => p.BlankMiningPool())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_MINING_GOVERNANCE");
        }

        [Fact]
        public void CreateContract_Throws_InvalidGovernanceAddress()
        {
            SetupBalance(0);
            SetupBlock(10);

            SetupCall(StakingToken, 0, "get_MiningGovernance", null, TransferResult.Transferred(Address.Zero));

            this.Invoking(p => p.BlankMiningPool())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_GOVERNANCE_ADDRESS");
        }

        [Fact]
        public void CreateContract_Throws_InvalidMiningDuration()
        {
            SetupBalance(0);
            SetupBlock(10);

            SetupCall(StakingToken, 0, "get_MiningGovernance", null, TransferResult.Transferred(MiningGovernance));
            SetupCall(MiningGovernance, 0, "get_MiningDuration", null, TransferResult.Failed());

            this.Invoking(p => p.BlankMiningPool())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_MINING_DURATION");
        }

        [Fact]
        public void CreateContract_Throws_InvalidDurationAmount()
        {
            SetupBalance(0);
            SetupBlock(10);

            SetupCall(StakingToken, 0, "get_MiningGovernance", null, TransferResult.Transferred(MiningGovernance));
            SetupCall(MiningGovernance, 0, "get_MiningDuration", null, TransferResult.Transferred(0ul));

            this.Invoking(p => p.BlankMiningPool())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_DURATION_AMOUNT");
        }

        [Fact]
        public void GetRewardForDuration_Success()
        {
            const ulong miningDuration = 100;
            UInt256 rewardRate = 10;
            UInt256 expected = 1000;

            var miningPool = CreateNewMiningPool();

            State.SetUInt64(PoolStateKeys.MiningDuration, miningDuration);
            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);

            miningPool.GetRewardForDuration().Should().Be(expected);
        }

        [Theory]
        [InlineData(99, 100, 99)]
        [InlineData(100, 100, 100)]
        [InlineData(101, 100, 100)]
        public void LastTimeRewardApplicable_Success(ulong currentBlock, ulong periodFinish, ulong expected)
        {
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodFinish);

            var miningPool = CreateNewMiningPool(currentBlock);

            miningPool.LatestBlockApplicable().Should().Be(expected);
        }

        [Theory]
        [InlineData(105, 101, 25_000, 1_000, 20_000_000)]
        [InlineData(200, 100, 10_000_000_000, 100_000_000, 100_000_000)]
        [InlineData(200, 110, 10_000_000_000, 100_000_000, 100_000_000)]
        [InlineData(200, 110, 0, 100_000_000, 0)]
        [InlineData(150, 100, 10_000_000_000, 100_000_000, 50_000_000)]
        [InlineData(101, 100, 100, 100, 100_000_000)]
        [InlineData(150, 149, 100, 100, 5_000_000_000)]
        public UInt256 GetRewardPerStakedToken_Success(ulong currentBlock, ulong lastUpdateBlock, UInt256 totalSupply,
            UInt256 rewardRate, UInt256 expected)
        {
            const ulong periodStart = 100;
            const ulong periodFinish = 200;

            var currentRewardPerToken = totalSupply == 0
                ? 0
                : (((lastUpdateBlock - periodStart) * rewardRate) * 100_000_000) / totalSupply;

            State.SetUInt256(PoolStateKeys.RewardPerStakedTokenLast, currentRewardPerToken);
            State.SetUInt256(PoolStateKeys.TotalSupply, totalSupply);
            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, lastUpdateBlock);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodFinish);

            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);

            var rewardPerToken = miningPool.GetRewardPerStakedToken();

            rewardPerToken.Should().Be(expected);

            return rewardPerToken;
        }

        [Theory]
        [InlineData(105, 101, 25_000_000_000, 25_000_000_000, 100_000_000, 500_000_000)]
        [InlineData(200, 100, 5_000, 5_000, 100, 10_000)]
        [InlineData(149, 100, 5_000, 5_000, 100, 4_900)]
        [InlineData(150, 149, 5_000, 5_000, 100, 5_000)]
        [InlineData(150, 149, 5_000, 10_000, 100, 2_500)]
        public void GetMiningRewards_Success(ulong currentBlock, ulong lastUpdateBlock, UInt256 minerAmount, UInt256 totalSupply,
            UInt256 rewardRate, UInt256 expected)
        {
            const ulong periodStart = 100;
            const ulong periodFinish = 200;

            var rewardPerToken = (((lastUpdateBlock - periodStart) * rewardRate) * 100_000_000) / totalSupply;

            State.SetUInt256(PoolStateKeys.RewardPerStakedTokenLast, rewardPerToken);
            State.SetUInt256(PoolStateKeys.TotalSupply, totalSupply);
            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, lastUpdateBlock);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodFinish);

            State.SetUInt256($"{PoolStateKeys.Balance}:{Miner1}", minerAmount);

            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);

            var earned = miningPool.GetMiningRewards(Miner1);

            earned.Should().Be(expected);
        }

        #region Start Mining Tests

        [Theory]
        [InlineData(101, 100, 0, 100_000_000, 100_000_000, 0)]
        [InlineData(105, 101, 25_000_000_000, 100_000_000, 100_000_000, 2_000_000)]
        [InlineData(200, 100, 10_000_000_000, 100_000_000, 100_000_000, 100_000_000)]
        public void StartMining_NewMiner_Success(ulong currentBlock, ulong lastUpdateBlock, UInt256 totalSupply,
            UInt256 rewardRate, UInt256 amount, UInt256 userRewardPerTokenPaid)
        {
            const ulong periodStart = 100;
            const ulong periodFinish = 200;

            var expectedTotalSupply = totalSupply + amount;
            var currentRewardPerToken = totalSupply == 0
                ? 0
                : (((lastUpdateBlock - periodStart) * rewardRate) * 100_000_000) / totalSupply;

            State.SetUInt256(PoolStateKeys.RewardPerStakedTokenLast, currentRewardPerToken);
            State.SetUInt256(PoolStateKeys.TotalSupply, totalSupply);
            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, lastUpdateBlock);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodFinish);

            var transferParams = new object[] { Miner1, MiningPool1, amount };
            SetupCall(Pool1, 0ul, nameof(IStandardToken256.TransferFrom), transferParams, TransferResult.Transferred(true));

            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            miningPool.StartMining(amount);

            miningPool.TotalSupply.Should().Be(expectedTotalSupply);
            miningPool.GetBalance(Miner1).Should().Be(amount);
            miningPool.GetStoredReward(Miner1).Should().Be(UInt256.Zero);
            miningPool.GetStoredRewardPerStakedToken(Miner1).Should().Be(userRewardPerTokenPaid);

            VerifyCall(Pool1, 0ul, nameof(IStandardToken256.TransferFrom), transferParams, Times.Once);
            VerifyLog(new MineLog { Miner = Miner1, Amount = amount, TotalSupply = expectedTotalSupply, EventType = (byte)MineEventType.StartMining }, Times.Once);
        }

        [Theory]
        [InlineData(101, 100, 0, 100_000_000, 100_000_000, 0, 0)]
        [InlineData(102, 101, 100_000_000, 100_000_000, 100_000_000, 200_000_000, 200_000_000)]
        [InlineData(103, 102, 100_000_000, 100_000_000, 150_000_000, 300_000_000, 450_000_000)]
        [InlineData(150, 101, 250_000_000, 100_000_000, 250_000_000, 2_000_000_000, 5_000_000_000)]
        [InlineData(200, 100, 10_000_000_000, 100_000_000, 100_000_000, 100_000_000, 100_000_000)]
        public void StartMining_AddToExistingPosition_Success(ulong currentBlock, ulong lastUpdateBlock,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 amount, UInt256 userRewardPerTokenPaid, UInt256 expectedReward)
        {
            const ulong periodStart = 100;
            const ulong periodFinish = 200;

            var expectedTotalSupply = totalSupply + amount;
            var currentRewardPerToken = totalSupply == 0
                ? 0
                : (((lastUpdateBlock - periodStart) * rewardRate) * 100_000_000) / totalSupply;

            State.SetUInt256(PoolStateKeys.RewardPerStakedTokenLast, currentRewardPerToken);
            State.SetUInt256(PoolStateKeys.TotalSupply, totalSupply);
            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, lastUpdateBlock);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodFinish);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Miner1}", amount);

            var transferParams = new object[] { Miner1, MiningPool1, amount };
            SetupCall(Pool1, 0ul, nameof(IStandardToken256.TransferFrom), transferParams, TransferResult.Transferred(true));

            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            miningPool.StartMining(amount);

            miningPool.TotalSupply.Should().Be(expectedTotalSupply);
            miningPool.GetBalance(Miner1).Should().Be(amount * 2); // previous amount + same amount added again
            miningPool.GetStoredReward(Miner1).Should().Be(expectedReward);
            miningPool.GetStoredRewardPerStakedToken(Miner1).Should().Be(userRewardPerTokenPaid);
            miningPool.LatestBlockApplicable().Should().Be(currentBlock);

            VerifyCall(Pool1, 0ul, nameof(IStandardToken256.TransferFrom), transferParams, Times.Once);
            VerifyLog(new MineLog { Miner = Miner1, Amount = amount, TotalSupply = expectedTotalSupply, EventType = (byte)MineEventType.StartMining }, Times.Once);
        }

        [Fact]
        public void StartMining_Throws_InvalidAmount()
        {
            var miningPool = CreateNewMiningPool();

            miningPool.Invoking(s => s.StartMining(UInt256.Zero))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_AMOUNT");
        }

        [Fact]
        public void StartMining_Throws_Locked()
        {
            var miningPool = CreateNewMiningPool();

            State.SetBool(PoolStateKeys.Locked, true);

            miningPool.Invoking(s => s.StartMining(123))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }

        #endregion

        #region Collect Mining Rewards Tests

        [Theory]
        [InlineData(150, 101, 25_000_000_000, 100_000_000, 100_000_000, 19_600_000)]
        [InlineData(200, 100, 10_000_000_000, 100_000_000, 100_000_000, 100_000_000)]
        [InlineData(125, 125, 10_000_000_000, 100_000_000, 100_000_000, 0)]
        [InlineData(126, 125, 10_000_000_000, 100_000_000, 100_000_000, 1_000_000)]
        public void Collect_Success(ulong currentBlock, ulong lastUpdateBlock, UInt256 totalSupply,
            UInt256 rewardRate, UInt256 amount, UInt256 expectedReward)
        {
            const ulong periodStart = 100;
            const ulong periodFinish = 200;

            State.SetUInt256(PoolStateKeys.RewardPerStakedTokenLast, UInt256.Zero);
            State.SetUInt256(PoolStateKeys.TotalSupply, totalSupply);
            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, lastUpdateBlock);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodFinish);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Miner1}", amount);

            var transferParams = new object[] { Miner1, expectedReward };
            SetupCall(StakingToken, 0ul, nameof(IStandardToken256.TransferTo), transferParams, TransferResult.Transferred(true));

            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            miningPool.CollectMiningRewards();

            miningPool.GetStoredReward(Miner1).Should().Be(UInt256.Zero);
            miningPool.LastUpdateBlock.Should().Be(currentBlock);

            if (expectedReward > 0)
            {
                VerifyCall(StakingToken, 0, nameof(IStandardToken256.TransferTo), transferParams, Times.Once);
                VerifyLog(new CollectMiningRewardsLog { Miner = Miner1, Amount = expectedReward }, Times.Once);
            }
            else
            {
                VerifyCall(StakingToken, 0, nameof(IStandardToken256.TransferTo), It.IsAny<object[]>(), Times.Never);
                VerifyLog(new CollectMiningRewardsLog { Miner = It.IsAny<Address>(), Amount = It.IsAny<UInt256>() }, Times.Never);
            }
        }

        [Fact]
        public void Collect_Throws_Locked()
        {
            var miningPool = CreateNewMiningPool(100);

            State.SetBool(PoolStateKeys.Locked, true);

            miningPool.Invoking(s => s.CollectMiningRewards())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }

        #endregion

        #region Stop Mining Tests

        [Theory]
        [InlineData(150, 101, 25_000_000_000, 100_000_000, 100_000_000, 19_600_000)]
        [InlineData(200, 100, 10_000_000_000, 100_000_000, 25_000_000, 100_000_000)]
        [InlineData(125, 125, 10_000_000_000, 100_000_000, 100_000_000, 0)]
        [InlineData(126, 125, 10_000_000_000, 100_000_000, 50_000_000, 1_000_000)]
        public void StopMining_Success(ulong currentBlock, ulong lastUpdateBlock, UInt256 totalSupply, UInt256 minerBalance,
            UInt256 minerWithdraw, UInt256 expectedReward)
        {
            const ulong periodStart = 100;
            const ulong periodFinish = 200;
            UInt256 rewardRate = 100_000_000;

            var expectedTotalSupply = totalSupply - minerWithdraw;

            State.SetUInt256(PoolStateKeys.RewardPerStakedTokenLast, UInt256.Zero);
            State.SetUInt256(PoolStateKeys.TotalSupply, totalSupply);
            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, lastUpdateBlock);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodFinish);
            State.SetUInt256($"{PoolStateKeys.Balance}:{Miner1}", minerBalance);

            var transferRewardParams = new object[] { Miner1, expectedReward };
            SetupCall(StakingToken, 0ul, nameof(IStandardToken256.TransferTo), transferRewardParams, TransferResult.Transferred(true));

            var transferStakingTokensParams = new object[] { Miner1, minerWithdraw };
            SetupCall(Pool1, 0ul, nameof(IStandardToken256.TransferTo), transferStakingTokensParams, TransferResult.Transferred(true));

            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            miningPool.StopMining(minerWithdraw);

            miningPool.GetStoredReward(Miner1).Should().Be(UInt256.Zero);
            miningPool.TotalSupply.Should().Be(expectedTotalSupply);
            miningPool.GetBalance(Miner1).Should().Be(minerBalance - minerWithdraw);
            miningPool.LastUpdateBlock.Should().Be(currentBlock);

            VerifyCall(Pool1, 0, nameof(IStandardToken256.TransferTo), transferStakingTokensParams, Times.Once);
            VerifyLog(new MineLog { Miner = Miner1, Amount = minerWithdraw, TotalSupply = expectedTotalSupply, EventType = (byte)MineEventType.StopMining}, Times.Once);

            if (expectedReward > 0)
            {
                VerifyCall(StakingToken, 0, nameof(IStandardToken256.TransferTo), transferRewardParams, Times.Once);
                VerifyLog(new CollectMiningRewardsLog { Miner = Miner1, Amount = expectedReward }, Times.Once);
            }
            else
            {
                VerifyCall(StakingToken, 0, nameof(IStandardToken256.TransferTo), transferRewardParams, Times.Never);
                VerifyLog(new CollectMiningRewardsLog { Miner = It.IsAny<Address>(), Amount = It.IsAny<UInt256>() }, Times.Never);
            }
        }

        [Fact]
        public void StopMining_Throws_Locked()
        {
            var miningPool = CreateNewMiningPool();

            State.SetBool(PoolStateKeys.Locked, true);

            miningPool.Invoking(s => s.StopMining(UInt256.MinValue))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }

        [Fact]
        public void StopMining_Throws_InvalidAmount()
        {
            var miningPool = CreateNewMiningPool();

            miningPool.Invoking(s => s.StopMining(UInt256.Zero))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_AMOUNT");
        }

        #endregion

        #region Notify Reward Amount Tests

        [Fact]
        public void NotifyRewardAmount_NotActive_Success()
        {
            const ulong block = 100;
            const ulong duration = 100;
            const ulong expectedMiningPeriodEndBlock = block + duration;
            UInt256 rewardAmount = 100_000;
            UInt256 expectedRewardRate = 1_000;
            UInt256 rewardRate = 1_000;

            var miningPool = CreateNewMiningPool();

            SetupBlock(block);
            SetupMessage(MiningPool1, MiningGovernance);

            State.SetUInt64(PoolStateKeys.MiningDuration, duration);
            State.SetAddress(PoolStateKeys.MinedToken, StakingToken);
            State.SetAddress(PoolStateKeys.MiningGovernance, MiningGovernance);

            var balanceParams = new object[] {MiningPool1};
            SetupCall(StakingToken, 0ul, nameof(IStandardToken.GetBalance), balanceParams, TransferResult.Transferred(rewardAmount));

            miningPool.NotifyRewardAmount(rewardAmount);

            miningPool.RewardRate.Should().Be(expectedRewardRate);
            miningPool.MiningPeriodEndBlock.Should().Be(block + duration);

            VerifyLog(new EnableMiningLog { Amount = rewardAmount, RewardRate = rewardRate, MiningPeriodEndBlock = expectedMiningPeriodEndBlock}, Times.Once);
        }

        [Fact]
        public void NotifyRewardAmount_Active_Success()
        {
            UInt256 rewardAmount = 100_000;
            UInt256 balance = 150_000;
            UInt256 rewardRate = 1_000;
            UInt256 newRewardRate = 1_500;
            const ulong duration = 100;
            const ulong startingBlock = 100;
            const ulong currentBlock = 150;
            const ulong endBlock = 200;

            var miningPool = CreateNewMiningPool(startingBlock);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, MiningGovernance);

            State.SetUInt64(PoolStateKeys.MiningDuration, duration);
            State.SetAddress(PoolStateKeys.MinedToken, StakingToken);
            State.SetAddress(PoolStateKeys.MiningGovernance, MiningGovernance);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, endBlock);
            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);

            var balanceParams = new object[] {MiningPool1};
            SetupCall(StakingToken, 0ul, nameof(IStandardToken.GetBalance), balanceParams, TransferResult.Transferred(balance));

            miningPool.NotifyRewardAmount(rewardAmount);

            miningPool.RewardRate.Should().Be(newRewardRate);
            miningPool.LastUpdateBlock.Should().Be(currentBlock);
            miningPool.MiningPeriodEndBlock.Should().Be(currentBlock + duration);

            VerifyLog(new EnableMiningLog { Amount = rewardAmount, RewardRate = newRewardRate, MiningPeriodEndBlock = currentBlock + duration }, Times.Once);
        }

        [Fact]
        public void NotifyRewardAmount_Throws_Locked()
        {
            var miningPool = CreateNewMiningPool();

            State.SetBool(PoolStateKeys.Locked, true);

            miningPool
                .Invoking(m => m.NotifyRewardAmount(100_000))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }

        [Fact]
        public void NotifyRewardAmount_Throws_Unauthorized()
        {
            var miningPool = CreateNewMiningPool();

            SetupMessage(MiningPool1, Pool1);

            miningPool
                .Invoking(m => m.NotifyRewardAmount(100_000))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }

        [Fact]
        public void NotifyRewardAmount_Throws_InvalidBalance()
        {
            const ulong duration = 100;

            var miningPool = CreateNewMiningPool();

            SetupMessage(MiningPool1, MiningGovernance);

            State.SetUInt64(PoolStateKeys.MiningDuration, duration);
            State.SetAddress(PoolStateKeys.MinedToken, StakingToken);
            State.SetAddress(PoolStateKeys.MiningGovernance, MiningGovernance);

            var balanceParams = new object[] {MiningPool1};
            SetupCall(StakingToken, 0ul, nameof(IStandardToken.GetBalance), balanceParams, TransferResult.Transferred(UInt256.Zero));

            miningPool
                .Invoking(m => m.NotifyRewardAmount(100_000))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_BALANCE");
        }

        [Fact]
        public void NotifyRewardAmount_Throws_RewardTooHigh()
        {
            const ulong duration = 100;
            UInt256 balance = 1;

            var miningPool = CreateNewMiningPool();

            SetupMessage(MiningPool1, MiningGovernance);

            State.SetUInt64(PoolStateKeys.MiningDuration, duration);
            State.SetAddress(PoolStateKeys.MinedToken, StakingToken);
            State.SetAddress(PoolStateKeys.MiningGovernance, MiningGovernance);

            var balanceParams = new object[] {MiningPool1};
            SetupCall(StakingToken, 0ul, nameof(IStandardToken.GetBalance), balanceParams, TransferResult.Transferred(balance));

            miningPool
                .Invoking(m => m.NotifyRewardAmount(UInt256.MaxValue))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: PROVIDED_REWARD_TOO_HIGH");
        }

        #endregion

        #region Maintain State Mining Tests

        [Fact]
        public void SingleMiner_FullLength_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;

            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodEnd);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            UInt256 miner1Amount = 100_000_000;
            UInt256 miner1Rewards = 10_000_000_000;

            // start mining
            StartMining(miningPool, Miner1, miner1Amount);

            // skip ahead to end
            SetupBlock(periodEnd);

            // stop mining
            StopMining(miningPool, Miner1, miner1Amount, miner1Rewards);
        }

        [Fact]
        public void TwoMiners_FullLength_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;

            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodEnd);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            UInt256 miner1Amount = 100_000_000;
            UInt256 miner2Amount = 200_000_000;

            // start mining - miner 1
            StartMining(miningPool, Miner1, miner1Amount);

            // start mining - miner 2
            StartMining(miningPool, Miner2, miner2Amount);

            // skip ahead to end
            SetupBlock(periodEnd);

            // stop mining - miner 1
            StopMining(miningPool, Miner1, miner1Amount, 3_333_333_333);

            // start mining - miner 2
            StopMining(miningPool, Miner2, miner2Amount, 6_666_666_666);
        }

        [Fact]
        public void SingleMiner_HalfLength_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;

            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodEnd);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            UInt256 miner1Amount = 100_000_000;
            UInt256 miner1Rewards = rewardRate * (periodEnd - periodStart) / 2;

            // starts half way
            SetupBlock(150);

            // start mining - miner 1
            StartMining(miningPool, Miner1, miner1Amount);

            // skip ahead to end
            SetupBlock(periodEnd);

            // stop mining - miner 1
            StopMining(miningPool, Miner1, miner1Amount, miner1Rewards);
        }

        [Fact]
        public void TwoMiners_OneFullLength_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;

            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodEnd);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            UInt256 miner1Amount = 100_000_000;
            UInt256 miner2Amount = 100_000_000;

            // start mining - miner 1
            StartMining(miningPool, Miner1, miner1Amount);

            // start mining - miner 2
            StartMining(miningPool, Miner2, miner2Amount);

            // skip ahead to block 150
            SetupBlock(150);

            // stop mining - miner 1
            StopMining(miningPool, Miner1, miner1Amount, 2_500_000_000);

            // skip ahead to end
            SetupBlock(periodEnd);

            // start mining - miner 2
            StopMining(miningPool, Miner2, miner2Amount, 7_500_000_000);
        }

        [Fact]
        public void TwoMiners_CollectingFullLength_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;

            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodEnd);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            UInt256 miner1Amount = 100_000_000;
            UInt256 miner2Amount = 300_000_000;

            // start mining - miner 1
            StartMining(miningPool, Miner1, miner1Amount);

            // start mining - miner 2
            StartMining(miningPool, Miner2, miner2Amount);

            // skip ahead to block 150
            SetupBlock(150);

            // collect mining - miner 1
            CollectMiningRewards(miningPool, Miner1, 1_250_000_000);

            // collect mining - miner 2
            CollectMiningRewards(miningPool, Miner2, 3_750_000_000);

            // skip ahead to end
            SetupBlock(periodEnd);

            // stop mining - miner 1
            StopMining(miningPool, Miner1, miner1Amount, 1_250_000_000);

            // stop mining - miner 2
            StopMining(miningPool, Miner2, miner2Amount, 3_750_000_000);
        }

        [Fact]
        public void TwoMiners_PartialWithdrawal_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;

            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodEnd);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            UInt256 miner1Amount = 100_000_000;
            UInt256 miner2Amount = 300_000_000;

            // start mining - miner 1
            StartMining(miningPool, Miner1, miner1Amount);

            // start mining - miner 2
            StartMining(miningPool, Miner2, miner2Amount);

            // skip ahead to block 150
            SetupBlock(150);

            // collect mining - miner 1
            CollectMiningRewards(miningPool, Miner1, 1_250_000_000);

            // stop mining - miner 2
            StopMining(miningPool, Miner2, miner2Amount / 2, 3_750_000_000);

            // skip ahead to end
            SetupBlock(periodEnd);

            // stop mining - miner 1
            StopMining(miningPool, Miner1, miner1Amount, 2_000_000_000);

            // stop mining - miner 2
            StopMining(miningPool, Miner2, miner2Amount / 2, 3_000_000_000);
        }

        [Fact]
        public void TwoMiners_AddToPosition_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;

            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodEnd);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            UInt256 miner1Amount = 100_000_000;
            UInt256 miner2Amount = 300_000_000;

            // start mining - miner 1
            StartMining(miningPool, Miner1, miner1Amount);

            // start mining - miner 2
            StartMining(miningPool, Miner2, miner2Amount);

            // skip ahead to block 150
            SetupBlock(150);

            // collect mining - miner 2
            CollectMiningRewards(miningPool, Miner2, 3_750_000_000);

            // Miner 1 add to position
            UInt256 miner1AdditionalAmount = 200_000_000;
            StartMining(miningPool, Miner1, miner1AdditionalAmount);

            // skip ahead to end
            SetupBlock(periodEnd);

            // stop mining - miner 1
            StopMining(miningPool, Miner1, miner1Amount + miner1AdditionalAmount, 3_749_999_999);

            // start mining - miner 2
            StopMining(miningPool, Miner2, miner2Amount, 2_499_999_999);
        }

        [Fact]
        public void TwoMiners_OneLargeAmount_WithSyncedDust_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;

            State.SetUInt256(PoolStateKeys.RewardRate, rewardRate);
            State.SetUInt64(PoolStateKeys.MiningPeriodEndBlock, periodEnd);
            State.SetUInt64(PoolStateKeys.LastUpdateBlock, periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            UInt256 miner1Amount = 1_000_000_000_000;
            UInt256 miner2Amount = 1_000;

            // miner 1 start
            StartMining(miningPool, Miner1, miner1Amount);

            // miner 2 start
            StartMining(miningPool, Miner2, miner2Amount);

            // skip ahead to end
            SetupBlock(periodEnd);

            // miner 2 stop
            StopMining(miningPool, Miner2, miner2Amount, 9);

            // miner 1 stop (rounding differences)
            StopMining(miningPool, Miner1, miner1Amount, 9_999_990_000);
        }

        private void StartMining(IOpdexMiningPool miningPool, Address miner, UInt256 amount)
        {
            SetupMessage(MiningPool1, miner);
            SetupCall(Pool1, 0, nameof(IOpdexLiquidityPool.TransferFrom), new object[] {miner, MiningPool1, amount}, TransferResult.Transferred(true));

            var currentBalance = miningPool.GetBalance(miner);
            var expectedTotalSupply = miningPool.TotalSupply + amount;

            miningPool.StartMining(amount);
            miningPool.GetBalance(miner).Should().Be(currentBalance + amount);
            miningPool.TotalSupply.Should().Be(expectedTotalSupply);

            VerifyCall(Pool1, 0, nameof(IOpdexLiquidityPool.TransferFrom), new object[] {miner, MiningPool1, amount}, Times.Once);
            VerifyLog(new MineLog {Miner = miner, Amount = amount, TotalSupply = expectedTotalSupply, EventType = (byte)MineEventType.StartMining}, Times.Once);
        }

        private void CollectMiningRewards(IOpdexMiningPool miningPool, Address miner, UInt256 rewards)
        {
            SetupMessage(MiningPool1, miner);
            SetupCall(StakingToken, 0, nameof(IOpdexLiquidityPool.TransferTo), new object[] {miner, rewards}, TransferResult.Transferred(true));

            miningPool.CollectMiningRewards();
            miningPool.GetStoredReward(miner).Should().Be(UInt256.Zero);

            VerifyCall(StakingToken, 0, nameof(IOpdexLiquidityPool.TransferTo), new object[] {miner, rewards}, Times.AtLeastOnce);
            VerifyLog(new CollectMiningRewardsLog {Miner = miner, Amount = rewards}, Times.AtLeastOnce);
        }

        private void StopMining(IOpdexMiningPool miningPool, Address miner, UInt256 amount, UInt256 rewards)
        {
            SetupMessage(MiningPool1, miner);
            SetupCall(Pool1, 0, nameof(IOpdexLiquidityPool.TransferTo), new object[] {miner, amount}, TransferResult.Transferred(true));
            SetupCall(StakingToken, 0, nameof(IOpdexLiquidityPool.TransferTo), new object[] {miner, rewards}, TransferResult.Transferred(true));

            var currentBalance = miningPool.GetBalance(miner);
            var expectedTotalSupply = miningPool.TotalSupply - amount;

            miningPool.StopMining(amount);
            miningPool.GetBalance(miner).Should().Be(currentBalance - amount);
            miningPool.GetMiningRewards(miner).Should().Be(UInt256.Zero);
            miningPool.TotalSupply.Should().Be(expectedTotalSupply);

            VerifyCall(Pool1, 0, nameof(IOpdexLiquidityPool.TransferTo), new object[] {miner, amount}, Times.AtLeastOnce);
            VerifyCall(StakingToken, 0, nameof(IOpdexLiquidityPool.TransferTo), new object[] {miner, rewards}, Times.AtLeastOnce);
            VerifyLog(new CollectMiningRewardsLog { Miner = miner, Amount = rewards }, Times.AtLeastOnce);
            VerifyLog(new MineLog { Miner = miner, Amount = amount, TotalSupply = expectedTotalSupply, EventType = (byte)MineEventType.StopMining }, Times.AtLeastOnce);
        }

        #endregion
    }
}
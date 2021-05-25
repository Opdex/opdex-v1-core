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
            miningPool.RewardPerToken.Should().Be(UInt256.Zero);
        }

        [Fact]
        public void GetRewardForDuration_Success()
        {
            const ulong miningDuration = 100;
            UInt256 rewardRate = 10;
            UInt256 expected = 1000;

            var miningPool = CreateNewMiningPool(100);
            
            State.SetUInt64(nameof(IOpdexMiningPool.MiningDuration), miningDuration);
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);

            miningPool.GetRewardForDuration().Should().Be(expected);
        }

        [Theory]
        [InlineData(99, 100, 99)]
        [InlineData(100, 100, 100)]
        [InlineData(101, 100, 100)]
        public void LastTimeRewardApplicable_Success(ulong currentBlock, ulong periodFinish, ulong expected)
        {
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var miningPool = CreateNewMiningPool(currentBlock);

            miningPool.LatestBlockApplicable().Should().Be(expected);
        }

        [Theory]
        [InlineData(100, 200, 105, 101, 25_000, 1_000, 20_000_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000)]
        [InlineData(100, 200, 200, 110, 10_000_000_000, 100_000_000, 100_000_000)]
        [InlineData(100, 200, 200, 110, 0, 100_000_000, 0)]
        [InlineData(100, 200, 150, 100, 10_000_000_000, 100_000_000, 50_000_000)]
        [InlineData(100, 200, 101, 100, 100, 100, 100_000_000)]
        [InlineData(100, 200, 150, 149, 100, 100, 5_000_000_000)]
        // rewardPerToken + ((LastBlockApplicable - LastUpdatedBlock) * RewardRate) * 100_000_000 / TotalSupply)
        public UInt256 RewardPerToken_Success(ulong periodStart, ulong periodFinish, ulong currentBlock,
            ulong lastUpdateBlock, UInt256 totalSupply, UInt256 rewardRate, UInt256 expected)
        {
            var currentRewardPerToken = totalSupply == 0 ? 0 : (((lastUpdateBlock - periodStart) * rewardRate) * 100_000_000) / totalSupply;
            State.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), currentRewardPerToken);
            State.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            State.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateBlock);
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);

            var rewardPerToken = miningPool.GetRewardPerToken();

            rewardPerToken.Should().Be(expected);

            return rewardPerToken;
        }

        [Theory]
        [InlineData(100, 200, 105, 101, 25_000_000_000, 25_000_000_000, 100_000_000, 500_000_000)]
        [InlineData(100, 200, 200, 100, 5_000, 5_000, 100, 10_000)]
        [InlineData(100, 200, 149, 100, 5_000, 5_000, 100, 4_900)]
        [InlineData(100, 200, 150, 149, 5_000, 5_000, 100, 5_000)]
        [InlineData(100, 200, 150, 149, 5_000, 10_000, 100, 2_500)]
        public void Earned_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateBlock,
            UInt256 minerAmount, UInt256 totalSupply, UInt256 rewardRate, UInt256 expected)
        {
            var rewardPerToken = (((lastUpdateBlock - periodStart) * rewardRate) * 100_000_000) / totalSupply;
            State.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), rewardPerToken);
            State.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            State.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateBlock);
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);
            
            State.SetUInt256($"Balance:{Miner1}", minerAmount);

            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);

            var earned = miningPool.Earned(Miner1);
            
            earned.Should().Be(expected);
        }

        [Theory]
        [InlineData(100, 200, 101, 100, 0, 100_000_000, 100_000_000, 0)]
        [InlineData(100, 200, 105, 101, 25_000_000_000, 100_000_000, 100_000_000, 2_000_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000, 100_000_000)]
        public void Mine_NewMiner_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateBlock,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 amount, UInt256 userRewardPerTokenPaid)
        {
            var currentRewardPerToken = totalSupply == 0 ? 0 : (((lastUpdateBlock - periodStart) * rewardRate) * 100_000_000) / totalSupply;
            State.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), currentRewardPerToken);
            State.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            State.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateBlock);
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var transferParams = new object[] { Miner1, MiningPool1, amount };
            SetupCall(Pool1, 0ul, "TransferFrom", transferParams, TransferResult.Transferred(true));
            
            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            miningPool.Mine(amount);

            miningPool.TotalSupply.Should().Be(totalSupply + amount);
            miningPool.GetBalance(Miner1).Should().Be(amount);
            miningPool.GetReward(Miner1).Should().Be(UInt256.Zero);
            miningPool.GetRewardPerTokenPaid(Miner1).Should().Be(userRewardPerTokenPaid);

            VerifyCall(Pool1, 0ul, "TransferFrom", transferParams, Times.Once);
            VerifyLog(new StartMiningLog { Miner = Miner1, Amount = amount }, Times.Once);
        }

        [Theory]
        [InlineData(100, 200, 101, 100, 0, 100_000_000, 100_000_000, 0, 0)]
        [InlineData(100, 200, 102, 101, 100_000_000, 100_000_000, 100_000_000, 200_000_000, 200_000_000)]
        [InlineData(100, 200, 103, 102, 100_000_000, 100_000_000, 150_000_000, 300_000_000, 450_000_000)]
        [InlineData(100, 200, 150, 101, 250_000_000, 100_000_000, 250_000_000, 2_000_000_000, 5_000_000_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000, 100_000_000, 100_000_000)]
        public void Mine_AddToExistingPosition_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateBlock,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 amount, UInt256 userRewardPerTokenPaid, UInt256 expectedReward)
        {
            var currentRewardPerToken = totalSupply == 0 ? 0 : (((lastUpdateBlock - periodStart) * rewardRate) * 100_000_000) / totalSupply;
            State.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), currentRewardPerToken);
            State.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            State.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateBlock);
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);
            State.SetUInt256($"Balance:{Miner1}", amount);

            var transferParams = new object[] { Miner1, MiningPool1, amount };
            SetupCall(Pool1, 0ul, "TransferFrom", transferParams, TransferResult.Transferred(true));
            
            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            miningPool.Mine(amount);

            miningPool.TotalSupply.Should().Be(totalSupply + amount);
            miningPool.GetBalance(Miner1).Should().Be(amount * 2); // previous amount + same amount added again
            miningPool.GetReward(Miner1).Should().Be(expectedReward);
            miningPool.GetRewardPerTokenPaid(Miner1).Should().Be(userRewardPerTokenPaid);

            VerifyCall(Pool1, 0ul, "TransferFrom", transferParams, Times.Once);
            VerifyLog(new StartMiningLog { Miner = Miner1, Amount = amount }, Times.Once);
        }

        [Fact]
        public void Mine_Throws_CannotMineZero()
        {
            var miningPool = CreateNewMiningPool(100);

            SetupBlock(101);
            SetupMessage(MiningPool1, Miner1);

            miningPool.Invoking(s => s.Mine(0))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: CANNOT_MINE_ZERO");
        }
        
        [Fact]
        public void Mine_Throws_Locked()
        {
            var miningPool = CreateNewMiningPool(100);

            SetupBlock(101);
            SetupMessage(MiningPool1, Miner1);
            
            State.SetBool(nameof(IOpdexMiningPool.Locked), true);

            miningPool.Invoking(s => s.Mine(123))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }
        
        [Theory]
        [InlineData(100, 200, 150, 101, 25_000_000_000, 100_000_000, 100_000_000, 19_600_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000, 100_000_000)]
        [InlineData(100, 200, 125, 125, 10_000_000_000, 100_000_000, 100_000_000, 0)]
        [InlineData(100, 200, 126, 125, 10_000_000_000, 100_000_000, 100_000_000, 1_000_000)]
        public void Collect_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateBlock,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 amount, UInt256 expectedReward)
        {
            State.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), UInt256.Zero);
            State.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            State.SetUInt256($"Balance:{Miner1}", amount);
            State.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateBlock);
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var transferParams = new object[] { Miner1, expectedReward };
            SetupCall(StakingToken, 0ul, nameof(IStandardToken256.TransferTo), transferParams, TransferResult.Transferred(true));
            
            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            miningPool.Collect();

            miningPool.GetReward(Miner1).Should().Be(UInt256.Zero);
            
            if (expectedReward > 0)
            {
                VerifyCall(StakingToken, 0, nameof(IStandardToken256.TransferTo), transferParams, Times.Once);
                VerifyLog(new CollectMiningRewardsLog { Miner = Miner1, Amount = expectedReward }, Times.Once);
            }
            else
            {
                VerifyCall(StakingToken, 0, nameof(IStandardToken256.TransferTo), transferParams, Times.Never);
                VerifyLog(new CollectMiningRewardsLog { Miner = It.IsAny<Address>(), Amount = It.IsAny<UInt256>() }, Times.Never);
            }
        }
        
        [Fact]
        public void Collect_Throws_Locked()
        {
            var miningPool = CreateNewMiningPool(100);
            
            State.SetBool(nameof(IOpdexMiningPool.Locked), true);

            miningPool.Invoking(s => s.Collect())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }
        
        [Theory]
        [InlineData(100, 200, 150, 101, 25_000_000_000, 100_000_000, 100_000_000, 19_600_000)]
        [InlineData(100, 200, 200, 100, 10_000_000_000, 100_000_000, 100_000_000, 100_000_000)]
        [InlineData(100, 200, 125, 125, 10_000_000_000, 100_000_000, 100_000_000, 0)]
        [InlineData(100, 200, 126, 125, 10_000_000_000, 100_000_000, 100_000_000, 1_000_000)]
        public void Exit_Success(ulong periodStart, ulong periodFinish, ulong currentBlock, ulong lastUpdateBlock,
            UInt256 totalSupply, UInt256 rewardRate, UInt256 minerBalance, UInt256 expectedReward)
        {
            State.SetUInt256(nameof(IOpdexMiningPool.RewardPerToken), UInt256.Zero);
            State.SetUInt256(nameof(IOpdexMiningPool.TotalSupply), totalSupply);
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            State.SetUInt256($"Balance:{Miner1}", minerBalance);
            State.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), lastUpdateBlock);
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodFinish);

            var transferRewardParams = new object[] { Miner1, expectedReward };
            SetupCall(StakingToken, 0ul, nameof(IStandardToken256.TransferTo), transferRewardParams, TransferResult.Transferred(true));
            
            var transferStakingTokensParams = new object[] { Miner1, minerBalance };
            SetupCall(Pool1, 0ul, nameof(IStandardToken256.TransferTo), transferStakingTokensParams, TransferResult.Transferred(true));
            
            var miningPool = CreateNewMiningPool(periodStart);

            SetupBlock(currentBlock);
            SetupMessage(MiningPool1, Miner1);

            miningPool.Exit();

            miningPool.GetReward(Miner1).Should().Be(UInt256.Zero);
            miningPool.TotalSupply.Should().Be(totalSupply - minerBalance);
            miningPool.GetBalance(Miner1).Should().Be(UInt256.Zero);
            
            VerifyCall(Pool1, 0, nameof(IStandardToken256.TransferTo), transferStakingTokensParams, Times.Once);
            VerifyLog(new StopMiningLog { Miner = Miner1, Amount = minerBalance }, Times.Once);

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
        public void Exit_Throws_Locked()
        {
            var miningPool = CreateNewMiningPool(100);
            
            State.SetBool(nameof(IOpdexMiningPool.Locked), true);

            miningPool.Invoking(s => s.Exit())
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: LOCKED");
        }

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
            
            State.SetUInt64(nameof(IOpdexMiningPool.MiningDuration), duration);
            State.SetAddress(nameof(IOpdexMiningPool.MinedToken), StakingToken);
            State.SetAddress(nameof(IOpdexMiningPool.MiningGovernance), MiningGovernance);

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
            
            State.SetUInt64(nameof(IOpdexMiningPool.MiningDuration), duration);
            State.SetAddress(nameof(IOpdexMiningPool.MinedToken), StakingToken);
            State.SetAddress(nameof(IOpdexMiningPool.MiningGovernance), MiningGovernance);
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), endBlock);
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);

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
            
            SetupBlock(100);
            SetupMessage(MiningPool1, MiningGovernance);
            
            State.SetBool(nameof(IOpdexMiningPool.Locked), true);

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
            
            SetupBlock(100);
            SetupMessage(MiningPool1, Pool1);
            
            State.SetAddress(nameof(IOpdexMiningPool.MiningGovernance), MiningGovernance);

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
            
            SetupBlock(100);
            SetupMessage(MiningPool1, MiningGovernance);
            
            State.SetUInt64(nameof(IOpdexMiningPool.MiningDuration), duration);
            State.SetAddress(nameof(IOpdexMiningPool.MinedToken), StakingToken);
            State.SetAddress(nameof(IOpdexMiningPool.MiningGovernance), MiningGovernance);

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
            
            SetupBlock(100);
            SetupMessage(MiningPool1, MiningGovernance);
            
            State.SetUInt64(nameof(IOpdexMiningPool.MiningDuration), duration);
            State.SetAddress(nameof(IOpdexMiningPool.MinedToken), StakingToken);
            State.SetAddress(nameof(IOpdexMiningPool.MiningGovernance), MiningGovernance);

            var balanceParams = new object[] {MiningPool1};
            SetupCall(StakingToken, 0ul, nameof(IStandardToken.GetBalance), balanceParams, TransferResult.Transferred(balance));
            
            miningPool
                .Invoking(m => m.NotifyRewardAmount(UInt256.MaxValue))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: PROVIDED_REWARD_TOO_HIGH");
        }
        
        #region Maintain State Tests
        
        [Fact]
        public void SingleMiner_FullLength_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;
            
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodEnd);
            State.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            // start mining
            UInt256 miner1Amount = 100_000_000;
            StartMining(miningPool, Miner1, miner1Amount, miner1Amount);

            // skip ahead to end
            SetupBlock(periodEnd);
            
            // stop mining
            UInt256 miner1Rewards = 10_000_000_000;
            StopMining(miningPool, Miner1, miner1Amount, miner1Rewards);
        }
        
        [Fact]
        public void TwoMiners_FullLength_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;
            
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodEnd);
            State.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            // start mining - miner 1
            UInt256 miner1Amount = 100_000_000;
            StartMining(miningPool, Miner1, miner1Amount, miner1Amount);
            
            // start mining - miner 2
            UInt256 miner2Amount = 200_000_000;
            StartMining(miningPool, Miner2, miner2Amount, miner2Amount);

            // skip ahead to end 
            SetupBlock(periodEnd);
            
            // stop mining - miner 1
            UInt256 miner1Rewards = 3_333_333_333;
            StopMining(miningPool, Miner1, miner1Amount, miner1Rewards);
            
            // start mining - miner 2
            UInt256 miner2Rewards = 6_666_666_666;
            StopMining(miningPool, Miner2, miner2Amount, miner2Rewards);
        }
        
        [Fact]
        public void SingleMiner_HalfLength_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;
            
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodEnd);
            State.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            // starts half way
            SetupBlock(150);
            
            // start mining - miner 1
            UInt256 miner1Amount = 100_000_000;
            StartMining(miningPool, Miner1, miner1Amount, miner1Amount);

            // skip ahead to end
            SetupBlock(periodEnd);
            
            // stop mining - miner 1
            UInt256 miner1Rewards = 5_000_000_000;
            StopMining(miningPool, Miner1, miner1Amount, miner1Rewards);
        }
        
        [Fact]
        public void TwoMiners_OneFullLength_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;
            
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodEnd);
            State.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            // start mining - miner 1
            UInt256 miner1Amount = 100_000_000;
            StartMining(miningPool, Miner1, miner1Amount, miner1Amount);
            
            // start mining - miner 2
            UInt256 miner2Amount = 100_000_000;
            StartMining(miningPool, Miner2, miner2Amount, miner2Amount);

            // skip ahead to block 150 
            SetupBlock(150);
            
            // stop mining - miner 1
            UInt256 miner1Rewards = 2_500_000_000;
            StopMining(miningPool, Miner1, miner1Amount, miner1Rewards);
            
            // skip ahead to end 
            SetupBlock(periodEnd);
            
            // start mining - miner 2
            UInt256 miner2Rewards = 7_500_000_000;
            StopMining(miningPool, Miner2, miner2Amount, miner2Rewards);
        }
        
        [Fact]
        public void TwoMiners_CollectingFullLength_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;
            
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodEnd);
            State.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            // start mining - miner 1
            UInt256 miner1Amount = 100_000_000;
            StartMining(miningPool, Miner1, miner1Amount, miner1Amount);
            
            // start mining - miner 2
            UInt256 miner2Amount = 300_000_000;
            StartMining(miningPool, Miner2, miner2Amount, miner2Amount);

            // skip ahead to block 150 
            SetupBlock(150);
            
            // collect mining - miner 1
            UInt256 miner1Rewards = 1_250_000_000;
            CollectMiningRewards(miningPool, Miner1, miner1Rewards);
            
            // collect mining - miner 1
            UInt256 miner2Rewards = 3_750_000_000;
            CollectMiningRewards(miningPool, Miner2, miner2Rewards);
            
            // skip ahead to end 
            SetupBlock(periodEnd);
            
            // stop mining - miner 1
            UInt256 miner1FinalRewards = 1_250_000_000;
            StopMining(miningPool, Miner1, miner1Amount, miner1FinalRewards);
            
            // start mining - miner 2
            UInt256 miner2FinalRewards = 3_750_000_000;
            StopMining(miningPool, Miner2, miner2Amount, miner2FinalRewards);
        }
        
        [Fact]
        public void TwoMiners_AddToPosition_Success()
        {
            const ulong periodStart = 100;
            const ulong periodEnd = 200;
            UInt256 rewardRate = 100_000_000;
            
            State.SetUInt256(nameof(IOpdexMiningPool.RewardRate), rewardRate);
            State.SetUInt64(nameof(IOpdexMiningPool.MiningPeriodEndBlock), periodEnd);
            State.SetUInt64(nameof(IOpdexMiningPool.LastUpdateBlock), periodStart);

            var miningPool = CreateNewMiningPool(periodStart);

            // start mining - miner 1
            UInt256 miner1Amount = 100_000_000;
            StartMining(miningPool, Miner1, miner1Amount, miner1Amount);
            
            // start mining - miner 2
            UInt256 miner2Amount = 300_000_000;
            StartMining(miningPool, Miner2, miner2Amount, miner2Amount);

            // skip ahead to block 150 
            SetupBlock(150);
            
            // collect mining - miner 1
            UInt256 miner2Rewards = 3_750_000_000;
            CollectMiningRewards(miningPool, Miner2, miner2Rewards);
            
            // Miner 1 add to position
            UInt256 miner1AdditionalAmount = 200_000_000;
            StartMining(miningPool, Miner1, miner1AdditionalAmount, miner1Amount + miner1AdditionalAmount);

            // skip ahead to end 
            SetupBlock(periodEnd);
            
            // stop mining - miner 1
            UInt256 miner1FinalRewards = 3_749_999_999;
            StopMining(miningPool, Miner1, miner1Amount + miner1AdditionalAmount, miner1FinalRewards);
            
            // start mining - miner 2
            UInt256 miner2FinalRewards = 2_499_999_999;
            StopMining(miningPool, Miner2, miner2Amount, miner2FinalRewards);
        }

        private void StartMining(IOpdexMiningPool miningPool, Address miner, UInt256 amount, UInt256 expectedBalance)
        {
            SetupMessage(MiningPool1, miner);
            SetupCall(Pool1, 0, nameof(IOpdexPool.TransferFrom), new object[] {miner, MiningPool1, amount}, TransferResult.Transferred(true));
            
            miningPool.Mine(amount);
            miningPool.GetBalance(miner).Should().Be(expectedBalance);
            
            VerifyCall(Pool1, 0, nameof(IOpdexPool.TransferFrom), new object[] {miner, MiningPool1, amount}, Times.Once);
            VerifyLog(new StartMiningLog {Miner = miner, Amount = amount}, Times.Once);
        }
        
        private void CollectMiningRewards(IOpdexMiningPool miningPool, Address miner, UInt256 rewards)
        {
            SetupMessage(MiningPool1, miner);
            SetupCall(StakingToken, 0, nameof(IOpdexPool.TransferTo), new object[] {miner, rewards}, TransferResult.Transferred(true));

            miningPool.Collect();
            miningPool.GetReward(miner).Should().Be(UInt256.Zero);
            
            VerifyCall(StakingToken, 0, nameof(IOpdexPool.TransferTo), new object[] {miner, rewards}, Times.AtLeastOnce);
            VerifyLog(new CollectMiningRewardsLog {Miner = miner, Amount = rewards}, Times.AtLeastOnce);
        }

        private void StopMining(IOpdexMiningPool miningPool, Address miner, UInt256 amount, UInt256 rewards)
        {
            SetupMessage(MiningPool1, miner);
            SetupCall(Pool1, 0, nameof(IOpdexPool.TransferTo), new object[] {miner, amount}, TransferResult.Transferred(true));
            SetupCall(StakingToken, 0, nameof(IOpdexPool.TransferTo), new object[] {miner, rewards}, TransferResult.Transferred(true));
            
            miningPool.Exit();
            miningPool.GetBalance(miner).Should().Be(UInt256.Zero);
            miningPool.Earned(miner).Should().Be(UInt256.Zero);
            
            VerifyCall(Pool1, 0, nameof(IOpdexPool.TransferTo), new object[] {miner, amount}, Times.AtLeastOnce);
            VerifyCall(StakingToken, 0, nameof(IOpdexPool.TransferTo), new object[] {miner, rewards}, Times.AtLeastOnce);
            VerifyLog(new CollectMiningRewardsLog { Miner = miner, Amount = rewards }, Times.AtLeastOnce);
            VerifyLog(new StopMiningLog { Miner = miner, Amount = amount }, Times.AtLeastOnce);
        }
        
        #endregion
    }
}
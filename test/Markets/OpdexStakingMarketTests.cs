using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexCoreContracts.Tests
{
    public class OpdexStakingMarketTests : BaseContractTest
    {
        [Fact]
        public void CreatesNewStakingMarket_Success()
        {
            var stakingMarket = CreateNewOpdexStakingMarket();

            stakingMarket.StakeToken.Should().Be(StakeToken);
            stakingMarket.Fee.Should().Be(3);
        }

        #region Pool

        [Fact]
        public void GetPool_Success()
        {
            var controller = CreateNewOpdexStakingMarket();
            State.SetContract(Pool, true);
            State.SetAddress($"Pool:{Token}", Pool);
            
            controller.GetPool(Token).Should().Be(Pool);
        }

        [Fact]
        public void CreatesPoolWithStakeToken_Success()
        {
            var controller = CreateNewOpdexStakingMarket();
            State.SetContract(Token, true);
            State.SetAddress(nameof(StakeToken), StakeToken);

            SetupCreate<OpdexStakingPool>(CreateResult.Succeeded(Pool), parameters: new object[] {Token, StakeToken, controller.Fee});

            var pool = controller.CreatePool(Token);

            controller.GetPool(Token)
                .Should().Be(pool)
                .And.Be(Pool);

            var expectedPoolCreatedLog = new LiquidityPoolCreatedLog { Token = Token, Pool = Pool };
            VerifyLog(expectedPoolCreatedLog, Times.Once);
        }

        [Fact]
        public void CreatesPool_Throws_ZeroAddress()
        {
            var token = Address.Zero;
            var controller = CreateNewOpdexStakingMarket();
            
            controller
                .Invoking(c => c.CreatePool(token))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: ZERO_ADDRESS");
        }
        
        [Fact]
        public void CreatesPool_Throws_PoolExists()
        {
            var controller = CreateNewOpdexStakingMarket();
            State.SetContract(Token, true);
            State.SetAddress($"Pool:{Token}", Pool);
            
            controller
                .Invoking(c => c.CreatePool(Token))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: POOL_EXISTS");
        }
        
        #endregion
    }
}
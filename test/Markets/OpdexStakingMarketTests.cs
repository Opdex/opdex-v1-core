using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexCoreContracts.Tests
{
    public class OpdexStakingMarketTests : TestBase
    {
        [Fact]
        public void CreatesNewStakingMarket_Success()
        {
            var market = CreateNewOpdexStakingMarket();

            market.StakingToken.Should().Be(StakingToken);
            market.Fee.Should().Be(3);
        }

        #region Pool

        [Fact]
        public void GetPool_Success()
        {
            var market = CreateNewOpdexStakingMarket();
            State.SetContract(Pool, true);
            State.SetAddress($"Pool:{Token}", Pool);
            
            market.GetPool(Token).Should().Be(Pool);
        }

        [Fact]
        public void CreatesPoolWithStakingToken_Success()
        {
            var market = CreateNewOpdexStakingMarket();
            
            State.SetContract(Token, true);
            State.SetAddress(nameof(StakingToken), StakingToken);

            SetupCreate<OpdexStakingPool>(CreateResult.Succeeded(Pool), parameters: new object[] {Token, StakingToken, market.Fee});

            var pool = market.CreatePool(Token);

            market.GetPool(Token).Should().Be(pool).And.Be(Pool);

            var expectedPoolCreatedLog = new LiquidityPoolCreatedLog { Token = Token, Pool = Pool };
            VerifyLog(expectedPoolCreatedLog, Times.Once);
        }

        [Fact]
        public void CreatesPool_Throws_InvalidToken()
        {
            var token = Address.Zero;
            var market = CreateNewOpdexStakingMarket();
            
            market
                .Invoking(c => c.CreatePool(token))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_TOKEN");
        }
        
        [Fact]
        public void CreatesPool_Throws_PoolExists()
        {
            var market = CreateNewOpdexStakingMarket();
            State.SetContract(Token, true);
            State.SetAddress($"Pool:{Token}", Pool);
            
            market
                .Invoking(c => c.CreatePool(Token))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: POOL_EXISTS");
        }
        
        #endregion
    }
}
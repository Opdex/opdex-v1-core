using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexCoreContracts.Tests
{
    public class OpdexMarketDeployerTests : TestBase
    {
        [Fact]
        public void CreatesOpdexMarketDeployer_Success()
        {
            CreateNewOpdexMarketDeployer();
            
            VerifyLog(new MarketCreatedLog { Market = StakingMarket }, Times.Once);
        }

        [Theory]
        [InlineData(false, false, false, 3)]
        [InlineData(true, false, false, 10)]
        [InlineData(false, true, false, 9)]
        [InlineData(false, true, true, 8)]
        [InlineData(true, true, false, 0)]
        [InlineData(true, true, true, 1)]
        public void CreateStandardMarket_Success(bool authPoolCreators, bool authProviders, bool authTraders, uint fee)
        {
            var createParams = new object[] {Owner, authPoolCreators, authProviders, authTraders, fee};
            SetupCreate<OpdexStandardMarket>(CreateResult.Succeeded(StandardMarket), 0, createParams);
            
            var deployer = CreateNewOpdexMarketDeployer();

            var market = deployer.CreateStandardMarket(authPoolCreators, authProviders, authTraders, fee);
            
            market.Should().Be(StandardMarket);
            
            VerifyLog(new MarketCreatedLog {Market = StandardMarket}, Times.Once);
        }
        
        [Fact]
        public void CreateStandardMarket_Throws_InvalidMarket()
        {
            var createParams = new object[] { Owner, true, true, true, 3U };
            SetupCreate<OpdexStandardMarket>(CreateResult.Failed(), 0, createParams);
            
            var deployer = CreateNewOpdexMarketDeployer();

            deployer
                .Invoking(d => d.CreateStandardMarket(true, true, true, 3))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_MARKET");
        }
    }
}
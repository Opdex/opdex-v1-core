using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexV1Core.Tests
{
    public class OpdexMarketDeployerTests : TestBase
    {
        [Fact]
        public void CreatesOpdexMarketDeployer_Success()
        {
            CreateNewOpdexMarketDeployer();
            
            VerifyLog(new CreateMarketLog
            {
                Market = StakingMarket, 
                Owner = Owner, 
                AuthPoolCreators = false, 
                AuthProviders = false, 
                AuthTraders = false, 
                Fee = 3U,
                StakingToken = StakingToken
            }, Times.Once);
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
            var marketOwner = Trader0;
            
            var createParams = new object[] {marketOwner, authPoolCreators, authProviders, authTraders, fee};
            SetupCreate<OpdexStandardMarket>(CreateResult.Succeeded(StandardMarket), 0, createParams);
            
            var deployer = CreateNewOpdexMarketDeployer();
            
            SetupMessage(Deployer, Owner);

            var market = deployer.CreateStandardMarket(marketOwner, authPoolCreators, authProviders, authTraders, fee);
            
            market.Should().Be(StandardMarket);
            
            VerifyLog(new CreateMarketLog
            {
                Market = StandardMarket, 
                Owner = marketOwner,
                AuthPoolCreators = authPoolCreators, 
                AuthProviders = authProviders, 
                AuthTraders = authTraders, 
                Fee = fee
            }, Times.Once);
        }
        
        [Fact]
        public void CreateStandardMarket_Throws_Unauthorized()
        {
            var deployer = CreateNewOpdexMarketDeployer();

            SetupMessage(Deployer, Trader1);
            
            deployer
                .Invoking(d => d.CreateStandardMarket(Owner, true, true, true, 3))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        [Fact]
        public void CreateStandardMarket_Throws_InvalidMarket()
        {
            var createParams = new object[] { Owner, true, true, true, 3U };
            SetupCreate<OpdexStandardMarket>(CreateResult.Failed(), 0, createParams);
            
            var deployer = CreateNewOpdexMarketDeployer();

            deployer
                .Invoking(d => d.CreateStandardMarket(Owner, true, true, true, 3))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_MARKET");
        }
        
        [Fact]
        public void SetOwner_Success()
        {
            var deployer = CreateNewOpdexMarketDeployer();

            State.SetAddress(nameof(IOpdexMarketDeployer.Owner), Owner);
            
            SetupMessage(Deployer, Owner);
            
            deployer.SetOwner(OtherAddress);

            deployer.Owner.Should().Be(OtherAddress);

            VerifyLog(new ChangeDeployerOwnerLog { From = Owner, To = OtherAddress }, Times.Once);
        }
        
        [Fact]
        public void SetOwner_Throws_Unauthorized()
        {
            var deployer = CreateNewOpdexMarketDeployer();

            SetupMessage(Deployer, Trader0);

            deployer
                .Invoking(m => m.SetOwner(OtherAddress))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
    }
}
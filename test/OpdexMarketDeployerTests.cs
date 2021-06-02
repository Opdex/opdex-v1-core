using FluentAssertions;
using Moq;
using OpdexV1Core.Tests.Base;
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
            var marketDeployer = CreateNewOpdexMarketDeployer();

            marketDeployer.Owner.Should().Be(Owner);
        }

        [Theory]
        [InlineData(false, false, false, 3, true)]
        [InlineData(true, false, false, 10, false)]
        [InlineData(false, true, false, 9, true)]
        [InlineData(false, true, true, 8, false)]
        [InlineData(true, true, false, 0, true)]
        [InlineData(true, true, true, 1, false)]
        public void CreateStandardMarket_Success(bool authPoolCreators, bool authProviders, bool authTraders, uint fee, bool enableMarketFee)
        {
            var marketOwner = Trader0;
            
            var createParams = new object[] {fee, marketOwner, authPoolCreators, authProviders, authTraders, enableMarketFee};
            SetupCreate<OpdexStandardMarket>(CreateResult.Succeeded(StandardMarket), 0, createParams);

            var createRouterParams = new object[] {StandardMarket, fee, authProviders, authTraders};
            SetupCreate<OpdexRouter>(CreateResult.Succeeded(Router), 0, createRouterParams);
            
            var deployer = CreateNewOpdexMarketDeployer();
            
            SetupMessage(Deployer, Owner);

            var market = deployer.CreateStandardMarket(marketOwner, fee, authPoolCreators, authProviders, authTraders, enableMarketFee);
            
            market.Should().Be(StandardMarket);
            
            VerifyLog(new CreateMarketLog
            {
                Market = StandardMarket, 
                Owner = marketOwner,
                Router = Router,
                AuthPoolCreators = authPoolCreators, 
                AuthProviders = authProviders, 
                AuthTraders = authTraders, 
                TransactionFee = fee,
                MarketFeeEnabled = enableMarketFee
            }, Times.Once);
        }

        [Fact]
        public void CreateStandardMarket_Throws_Unauthorized()
        {
            var deployer = CreateNewOpdexMarketDeployer();

            SetupMessage(Deployer, Trader1);
            
            deployer
                .Invoking(d => d.CreateStandardMarket(Owner, 3, true, true, true, false))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        [Fact]
        public void CreateStandardMarket_Throws_InvalidMarket()
        {
            var createParams = new object[] { 3U, Owner, true, true, true, false };
            SetupCreate<OpdexStandardMarket>(CreateResult.Failed(), 0, createParams);
            
            var deployer = CreateNewOpdexMarketDeployer();

            deployer
                .Invoking(d => d.CreateStandardMarket(Owner, 3, true, true, true, false))
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
        
        [Fact]
        public void CreateStakingMarket_Success()
        {
            const uint transactionFee = 3;
            var marketOwner = Owner;
            
            var createParams = new object[] {transactionFee, StakingToken};
            SetupCreate<OpdexStakingMarket>(CreateResult.Succeeded(StakingMarket), 0, createParams);

            var createRouterParams = new object[] {StakingMarket, transactionFee, false, false};
            SetupCreate<OpdexRouter>(CreateResult.Succeeded(Router), 0, createRouterParams);
            
            var deployer = CreateNewOpdexMarketDeployer();
            
            SetupMessage(Deployer, marketOwner);

            State.SetContract(StakingToken, true);

            var market = deployer.CreateStakingMarket(StakingToken);
            
            market.Should().Be(StakingMarket);
            
            VerifyLog(new CreateMarketLog
            {
                Market = StakingMarket, 
                Owner = marketOwner,
                Router = Router, 
                TransactionFee = transactionFee,
                StakingToken = StakingToken
            }, Times.Once);
        }
        
        [Fact]
        public void CreateStakingMarket_Throws_Unauthorized()
        {
            var deployer = CreateNewOpdexMarketDeployer();

            SetupMessage(Deployer, Trader1);
            
            deployer
                .Invoking(d => d.CreateStakingMarket(StakingToken))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        [Fact]
        public void CreateStakingMarket_Throws_InvalidMarket()
        {
            var createParams = new object[] { 3U, StakingToken };
            SetupCreate<OpdexStakingMarket>(CreateResult.Failed(), 0, createParams);
            
            State.SetContract(StakingToken, true);
            
            var deployer = CreateNewOpdexMarketDeployer();

            deployer
                .Invoking(d => d.CreateStakingMarket(StakingToken))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_MARKET");
        }
        
        [Fact]
        public void CreateStakingMarket_Throws_InvalidStakingToken()
        {
            State.SetContract(StakingToken, false);
            
            var deployer = CreateNewOpdexMarketDeployer();

            deployer
                .Invoking(d => d.CreateStakingMarket(StakingToken))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_STAKING_TOKEN");
        }
    }
}
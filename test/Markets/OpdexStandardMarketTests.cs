using FluentAssertions;
using Moq;
using OpdexV1Core.Tests.Base;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexV1Core.Tests.Markets
{
    public class OpdexStandardMarketTests : TestBase
    {
        [Theory]
        [InlineData(false, false, false, 3, true)]
        [InlineData(true, false, false, 10, false)]
        [InlineData(false, true, false, 9, true)]
        [InlineData(false, true, true, 8, false)]
        [InlineData(true, true, false, 0, true)]
        [InlineData(true, true, true, 1, false)]
        public void CreatesNewStandardMarket_Success(bool authPoolCreators, bool authProviders, bool authTraders, uint fee, bool marketFeeEnabled)
        {
            var market = CreateNewOpdexStandardMarket(authPoolCreators, authProviders, authTraders, fee, 0, marketFeeEnabled);

            market.AuthPoolCreators.Should().Be(authPoolCreators);
            market.AuthProviders.Should().Be(authProviders);
            market.AuthTraders.Should().Be(authTraders);
            market.TransactionFee.Should().Be(fee);
            market.MarketFeeEnabled.Should().Be(marketFeeEnabled);
        }

        #region Set Owner
        
        [Fact]
        public void SetOwner_Success()
        {
            var market = CreateNewOpdexStandardMarket();
            
            market.SetOwner(OtherAddress);

            market.Owner.Should().Be(OtherAddress);

            VerifyLog(new ChangeMarketOwnerLog { From = Owner, To = OtherAddress }, Times.Once);
        }
        
        [Fact]
        public void SetOwner_Throws_Unauthorized()
        {
            var market = CreateNewOpdexStandardMarket();

            SetupMessage(StandardMarket, Trader0);

            market
                .Invoking(m => m.SetOwner(OtherAddress))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        #endregion
        
        #region Authorizations

        [Theory]
        [InlineData((byte)Permissions.SetPermissions, true)]
        [InlineData((byte)Permissions.SetPermissions, false)]
        [InlineData((byte)Permissions.CreatePool, true)]
        [InlineData((byte)Permissions.CreatePool, false)]
        [InlineData((byte)Permissions.Provide, true)]
        [InlineData((byte)Permissions.Provide, false)]
        [InlineData((byte)Permissions.Trade, true)]
        [InlineData((byte)Permissions.Trade, false)]
        public void SetAuthorization_Success_AsOwner(byte permission, bool isAuthorized)
        {
            var authPoolCreators = (Permissions)permission == Permissions.CreatePool;
            var authProviders = (Permissions)permission == Permissions.Provide;
            var authTraders = (Permissions)permission == Permissions.Trade;
            
            var market = CreateNewOpdexStandardMarket(authPoolCreators, authProviders, authTraders);
            
            market.Authorize(OtherAddress, permission, isAuthorized);

            market.IsAuthorized(OtherAddress, permission).Should().Be(isAuthorized);

            VerifyLog(new ChangeMarketPermissionLog
            {
                Address = OtherAddress,
                Permission = permission,
                IsAuthorized = isAuthorized
            }, Times.Once);
        }
        
        [Theory]
        [InlineData((byte)Permissions.SetPermissions, true)]
        [InlineData((byte)Permissions.SetPermissions, false)]
        [InlineData((byte)Permissions.CreatePool, true)]
        [InlineData((byte)Permissions.CreatePool, false)]
        [InlineData((byte)Permissions.Provide, true)]
        [InlineData((byte)Permissions.Provide, false)]
        [InlineData((byte)Permissions.Trade, true)]
        [InlineData((byte)Permissions.Trade, false)]
        public void SetAuthorization_Success_AsPermittedAddress(byte permission, bool isAuthorized)
        {
            var authPoolCreators = (Permissions)permission == Permissions.CreatePool;
            var authProviders = (Permissions)permission == Permissions.Provide;
            var authTraders = (Permissions)permission == Permissions.Trade;
            
            var market = CreateNewOpdexStandardMarket(authPoolCreators, authProviders, authTraders);

            SetupMessage(StandardMarket, Owner);
            
            market.Authorize(OtherAddress, (byte)Permissions.SetPermissions, true);
            
            SetupMessage(StandardMarket, OtherAddress);
            
            market.Authorize(Trader0, permission, isAuthorized);

            market.IsAuthorized(Trader0, permission).Should().Be(isAuthorized);

            VerifyLog(new ChangeMarketPermissionLog
            {
                Address = OtherAddress,
                Permission = (byte)Permissions.SetPermissions,
                IsAuthorized = true
            }, Times.Once);
            
            VerifyLog(new ChangeMarketPermissionLog
            {
                Address = Trader0,
                Permission = permission,
                IsAuthorized = isAuthorized
            }, Times.Once);
        }

        [Fact]
        public void SetAuthorization_Throws_Unauthorized()
        {
            var market = CreateNewOpdexStandardMarket(true, true, true);

            SetupMessage(StandardMarket, Trader0);
            
            market
                .Invoking(m => m.Authorize(Trader0, (byte) Permissions.SetPermissions, true))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        [Fact]
        public void SetAuthorization_Throws_InvalidPermission()
        {
            var market = CreateNewOpdexStandardMarket(true, true, true);

            SetupMessage(StandardMarket, Owner);
            
            market
                .Invoking(m => m.Authorize(Trader0, (byte)Permissions.Unknown, true))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_PERMISSION");
        }
        
        [Theory]
        [InlineData(true, (byte)Permissions.Provide)]
        [InlineData(false, (byte)Permissions.Provide)]
        [InlineData(true, (byte)Permissions.Trade)]
        [InlineData(false, (byte)Permissions.Trade)]
        [InlineData(true, (byte)Permissions.CreatePool)]
        [InlineData(false, (byte)Permissions.CreatePool)]
        [InlineData(true, (byte)Permissions.SetPermissions)]
        public void IsAuthorized_SingleAddress_Authorized_Success(bool authAddress, byte permission)
        {
            var market = CreateNewOpdexStandardMarket(authAddress, authAddress, authAddress);

            if (authAddress)
            {
                State.SetBool($"IsAuthorized:{permission}:{Trader0}", true);
            }
            
            SetupMessage(StandardMarket, Trader0);

            market.IsAuthorized(Trader0, permission).Should().BeTrue();
        }
        
        [Theory]
        [InlineData((byte)Permissions.Provide)]
        [InlineData((byte)Permissions.Trade)]
        [InlineData((byte)Permissions.CreatePool)]
        [InlineData((byte)Permissions.SetPermissions)]
        [InlineData((byte)Permissions.Unknown)]
        public void IsAuthorized_SingleAddress_Unauthorized_Success(byte permission)
        {
            var market = CreateNewOpdexStandardMarket(true, true, true);

            SetupMessage(StandardMarket, Trader0);

            market.IsAuthorized(Trader0, permission).Should().BeFalse();
        }

        [Theory]
        [InlineData(true, (byte)Permissions.Provide)]
        [InlineData(false, (byte)Permissions.Provide)]
        [InlineData(true, (byte)Permissions.Trade)]
        [InlineData(false, (byte)Permissions.Trade)]
        [InlineData(true, (byte)Permissions.CreatePool)]
        [InlineData(false, (byte)Permissions.CreatePool)]
        [InlineData(true, (byte)Permissions.SetPermissions)]
        public void IsAuthorized_MultipleAddresses_Success(bool authAddress, byte permission)
        {
            var market = CreateNewOpdexStandardMarket(authAddress, authAddress, authAddress);

            if (authAddress)
            {
                State.SetBool($"IsAuthorized:{permission}:{Trader0}", true);
                State.SetBool($"IsAuthorized:{permission}:{Trader1}", true);
            }
            
            SetupMessage(StandardMarket, Trader0);

            market.IsAuthorized(Trader0, Trader1, permission).Should().BeTrue();
        }
        
        [Theory]
        [InlineData((byte)Permissions.Provide)]
        [InlineData((byte)Permissions.Trade)]
        [InlineData((byte)Permissions.CreatePool)]
        [InlineData((byte)Permissions.SetPermissions)]
        [InlineData((byte)Permissions.Unknown)]
        public void IsAuthorized_MultipleAddresses_Unauthorized_Success(byte permission)
        {
            var market = CreateNewOpdexStandardMarket(true, true, true);

            SetupMessage(StandardMarket, Trader0);

            market.IsAuthorized(Trader0, permission).Should().BeFalse();
        }
        
        #endregion
        
        #region Pools

        [Theory]
        [InlineData(false, false, 3)]
        [InlineData(false, true, 10)]
        [InlineData(true, false, 9)]
        [InlineData(false, true, 8)]
        [InlineData(true, false, 0)]
        [InlineData(true, true, 1)]
        public void CreateStandardPool_Success(bool authProviders, bool authTraders, uint fee)
        {
            const bool authPoolCreators = false;
            
            var market = CreateNewOpdexStandardMarket(authPoolCreators, authProviders, authTraders, fee);
            
            State.SetContract(Token, true);

            var parameters = new object[] { Token, market.TransactionFee, market.AuthProviders, market.AuthTraders, false };
            
            SetupCreate<OpdexStandardPool>(CreateResult.Succeeded(Pool), parameters: parameters);

            var pool = market.CreatePool(Token);

            market.GetPool(Token).Should().Be(pool).And.Be(Pool);
            market.AuthProviders.Should().Be(authProviders);
            market.AuthTraders.Should().Be(authTraders);
            market.TransactionFee.Should().Be(fee);
            market.Owner.Should().Be(Owner);

            var expectedPoolCreatedLog = new CreateLiquidityPoolLog { Token = Token, Pool = Pool };
            
            VerifyLog(expectedPoolCreatedLog, Times.Once);
        }
        
        [Fact]
        public void CreatesNewStandardPool_Throws_Unauthorized()
        {
            var unauthorizedUser = Trader0;
            
            var market = CreateNewOpdexStandardMarket(authPoolCreators: true);
            
            State.SetContract(Token, true);

            var parameters = new object[] { Token, false, false, market.TransactionFee };
            
            SetupCreate<OpdexStandardPool>(CreateResult.Succeeded(Pool), parameters: parameters);

            SetupMessage(StandardMarket, unauthorizedUser);
            
            market
                .Invoking(p => p.CreatePool(Token))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        #endregion
    }
}
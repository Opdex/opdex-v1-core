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
        [InlineData(true, true, false, 0, false)]
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

        [Fact]
        public void CreatesNewStandardMarket_ZeroFee_FeesEnabled_Throws_InvalidMarketFee()
        {
            const uint transactionFee = 0;
            const bool marketFeeEnabled = true;
            const ulong balance = 0;

            this
                .Invoking(c => c.CreateNewOpdexStandardMarket(true, true, true, transactionFee, balance, marketFeeEnabled))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_MARKET_FEE");
        }

        [Fact]
        public void CreatesNewStandardMarket_FeeTooHigh_Throws_InvalidMarketFee()
        {
            const uint transactionFee = 11;
            const bool marketFeeEnabled = true;
            const ulong balance = 0;

            this
                .Invoking(c => c.CreateNewOpdexStandardMarket(true, true, true, transactionFee, balance, marketFeeEnabled))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_TRANSACTION_FEE");
        }

        #region Set Owner

        [Fact]
        public void SetPendingOwnership_Success()
        {
            var market = CreateNewOpdexStandardMarket();

            State.SetAddress(MarketStateKeys.Owner, Owner);

            SetupMessage(Deployer, Owner);

            market.SetPendingOwnership(OtherAddress);

            market.PendingOwner.Should().Be(OtherAddress);

            VerifyLog(new SetPendingMarketOwnershipLog { From = Owner, To = OtherAddress }, Times.Once);
        }

        [Fact]
        public void SetPendingOwnership_Throws_Unauthorized()
        {
            var market = CreateNewOpdexStandardMarket();

            SetupMessage(Deployer, Trader0);

            market
                .Invoking(m => m.SetPendingOwnership(OtherAddress))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }

        [Fact]
        public void ClaimPendingOwnership_Success()
        {
            var pendingOwner = OtherAddress;
            var market = CreateNewOpdexStandardMarket();

            State.SetAddress(MarketStateKeys.Owner, Owner);
            State.SetAddress(MarketStateKeys.PendingOwner, pendingOwner);

            SetupMessage(Deployer, pendingOwner);

            market.ClaimPendingOwnership();

            market.PendingOwner.Should().Be(Address.Zero);
            market.Owner.Should().Be(pendingOwner);

            VerifyLog(new ClaimPendingMarketOwnershipLog { From = Owner, To = pendingOwner }, Times.Once);
        }

        [Fact]
        public void ClaimPendingOwnership_Throws_Unauthorized()
        {
            var market = CreateNewOpdexStandardMarket();

            State.SetAddress(MarketStateKeys.Owner, Owner);
            State.SetAddress(MarketStateKeys.PendingOwner, OtherAddress);

            SetupMessage(Deployer, Trader0);

            market
                .Invoking(m => m.ClaimPendingOwnership())
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

        [Theory]
        [InlineData((byte)Permissions.Unknown)]
        [InlineData(9)]
        public void SetAuthorization_Throws_InvalidPermission(byte permission)
        {
            var market = CreateNewOpdexStandardMarket(true, true, true);

            SetupMessage(StandardMarket, Owner);

            market
                .Invoking(m => m.Authorize(Trader0, permission, true))
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
                State.SetBool($"{MarketStateKeys.IsAuthorized}:{permission}:{Trader0}", true);
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
                State.SetBool($"{MarketStateKeys.IsAuthorized}:{permission}:{Trader0}", true);
                State.SetBool($"{MarketStateKeys.IsAuthorized}:{permission}:{Trader1}", true);
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

        #region Collect Market Fees

        [Fact]
        public void CollectMarketFees_Success_TransferFees()
        {
            UInt256 amount = 250;

            var market = CreateNewOpdexStandardMarket(marketFeeEnabled: true);

            State.SetAddress($"{MarketStateKeys.Pool}:{Token}", Pool);

            var transferParams = new object[] {Owner, amount};
            SetupCall(Pool, 0, nameof(IOpdexLiquidityPool.TransferTo), transferParams, TransferResult.Transferred(true));

            SetupMessage(StandardMarket, Owner);

            market.CollectMarketFees(Token, amount);

            VerifyCall(Pool, 0, nameof(IOpdexLiquidityPool.TransferTo), transferParams, Times.Once);
        }

        [Fact]
        public void CollectMarketFees_Success_MarketFeesDisabled()
        {
            UInt256 amount = 765;

            var market = CreateNewOpdexStandardMarket(marketFeeEnabled: false);

            State.SetAddress($"{MarketStateKeys.Pool}:{Token}", Pool);

            SetupMessage(StandardMarket, Owner);

            market.CollectMarketFees(Token, amount);

            VerifyCall(It.IsAny<Address>(), It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<object[]>(), Times.Never);
        }

        [Fact]
        public void CollectMarketFees_Success_ZeroAmount()
        {
            UInt256 amount = 0;

            var market = CreateNewOpdexStandardMarket(marketFeeEnabled: true);

            State.SetAddress($"{MarketStateKeys.Pool}:{Token}", Pool);

            SetupMessage(StandardMarket, Owner);

            market.CollectMarketFees(Token, amount);

            VerifyCall(It.IsAny<Address>(), It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<object[]>(), Times.Never);
        }

        [Fact]
        public void CollectMarketFees_Unauthorized()
        {
            UInt256 amount = 763;

            var market = CreateNewOpdexStandardMarket(marketFeeEnabled: true);

            State.SetAddress($"{MarketStateKeys.Pool}:{Token}", Pool);

            SetupMessage(StandardMarket, Trader0);

            market
                .Invoking(m => m.CollectMarketFees(Token, amount))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }

        [Fact]
        public void CollectMarketFees_InvalidPool()
        {
            UInt256 amount = 763;

            var market = CreateNewOpdexStandardMarket(marketFeeEnabled: true);

            State.SetAddress($"{MarketStateKeys.Pool}:{Token}", Address.Zero);

            SetupMessage(StandardMarket, Owner);

            market
                .Invoking(m => m.CollectMarketFees(Token, amount))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }

        [Fact]
        public void CollectMarketFees_Throws_InvalidTransferTo()
        {
            UInt256 amount = 250;

            var market = CreateNewOpdexStandardMarket(marketFeeEnabled: true);

            State.SetAddress($"{MarketStateKeys.Pool}:{Token}", Pool);

            var transferParams = new object[] {Owner, amount};
            SetupCall(Pool, 0, nameof(IOpdexLiquidityPool.TransferTo), transferParams, TransferResult.Failed());

            SetupMessage(StandardMarket, Owner);

            market
                .Invoking(m => m.CollectMarketFees(Token, amount))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_TRANSFER_TO");
        }

        #endregion
    }
}
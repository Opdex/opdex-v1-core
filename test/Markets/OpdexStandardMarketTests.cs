using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;
using Xunit.Sdk;

namespace OpdexCoreContracts.Tests
{
    public class OpdexStandardMarketTests : TestBase
    {
        [Theory]
        [InlineData(false, false, false, 3)]
        [InlineData(true, false, false, 10)]
        [InlineData(false, true, false, 9)]
        [InlineData(false, true, true, 8)]
        [InlineData(true, true, false, 0)]
        [InlineData(true, true, true, 1)]
        public void CreatesNewStandardMarket_Success(bool authPoolCreators, bool authProviders, bool authTraders, uint fee)
        {
            var market = CreateNewOpdexStandardMarket(authPoolCreators, authProviders, authTraders, fee);

            market.AuthPoolCreators.Should().Be(authPoolCreators);
            market.AuthProviders.Should().Be(authProviders);
            market.AuthTraders.Should().Be(authTraders);
            market.Fee.Should().Be(fee);
        }

        [Fact]
        public void UpdateOwner_Success()
        {
            var market = CreateNewOpdexStandardMarket();
            
            market.SetOwner(OtherAddress);

            market.Owner.Should().Be(OtherAddress);

            VerifyLog(new MarketOwnerChangeLog
            {
                From = Owner,
                To = OtherAddress
            }, Times.Once);
        }
        
        [Fact]
        public void UpdateOwner_Throws_Unauthorized()
        {
            var market = CreateNewOpdexStandardMarket();

            SetupMessage(StandardMarket, Trader0);

            market
                .Invoking(m => m.SetOwner(OtherAddress))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
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
        public void SetAuthorization_Success_AsOwner(byte permission, bool isAuthorized)
        {
            var authPoolCreators = (Permissions)permission == Permissions.CreatePool;
            var authProviders = (Permissions)permission == Permissions.Provide;
            var authTraders = (Permissions)permission == Permissions.Trade;
            
            var market = CreateNewOpdexStandardMarket(authPoolCreators, authProviders, authTraders);
            
            market.Authorize(OtherAddress, permission, isAuthorized);

            market.IsAuthorizedFor(OtherAddress, permission).Should().Be(isAuthorized);

            VerifyLog(new PermissionsChangeLog
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

            market.IsAuthorizedFor(Trader0, permission).Should().Be(isAuthorized);

            VerifyLog(new PermissionsChangeLog
            {
                Address = OtherAddress,
                Permission = (byte)Permissions.SetPermissions,
                IsAuthorized = true
            }, Times.Once);
            
            VerifyLog(new PermissionsChangeLog
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

            var parameters = new object[] { Token, market.AuthProviders, market.AuthTraders, market.Fee };
            
            SetupCreate<OpdexStandardPool>(CreateResult.Succeeded(Pool), parameters: parameters);

            var pool = market.CreatePool(Token);

            market.GetPool(Token).Should().Be(pool).And.Be(Pool);
            market.AuthProviders.Should().Be(authProviders);
            market.AuthTraders.Should().Be(authTraders);
            market.Fee.Should().Be(fee);
            market.Owner.Should().Be(Owner);

            var expectedPoolCreatedLog = new LiquidityPoolCreatedLog { Token = Token, Pool = Pool };
            
            VerifyLog(expectedPoolCreatedLog, Times.Once);
        }
        
        [Fact]
        public void CreatesNewStandardPool_Throws_Unauthorized()
        {
            var unauthorizedUser = Trader0;
            
            var market = CreateNewOpdexStandardMarket(authPoolCreators: true);
            
            State.SetContract(Token, true);

            var parameters = new object[] { Token, false, false, market.Fee };
            
            SetupCreate<OpdexStandardPool>(CreateResult.Succeeded(Pool), parameters: parameters);

            SetupMessage(Controller, unauthorizedUser);
            
            market
                .Invoking(p => p.CreatePool(Token))
                .Should()
                .Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        #endregion
        
        #region Add Liquidity

        [Theory]
        [InlineData(1_000, 10_000, 990, 9_900)]
        [InlineData(50000000, 950000000, 45000000, 85000000)]
        public void AddLiquidity_Success_NoReserves(ulong amountCrsDesired, UInt256 amountSrcDesired, ulong amountCrsMin, UInt256 amountSrcMin)
        {
            var to = OtherAddress;
            
            // Tests specific flows where there are no existing reserves
            const ulong expectedReserveCrs = 0;
            var expectedReserveSrc = UInt256.MinValue;
            
            var market = CreateNewOpdexStandardMarket();

            State.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress, amountCrsDesired);

            // Call to get reserves from pool
            var expectedReserves = new object[] { expectedReserveCrs, expectedReserveSrc };
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));
            
            // Transfer SRC to Pool
            var transferFromParams = new object[] {OtherAddress, Pool, amountSrcDesired};
            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));

            // Mint Liquidity Src
            var mintParams = new object[] {to};
            // It is not this tests responsibility to validate the minted liquidity tokens amounts
            SetupCall(Pool, amountCrsDesired, nameof(IOpdexStandardPool.Mint), mintParams, TransferResult.Transferred(It.IsAny<UInt256>()));

            var addLiquidityResponse = market.AddLiquidity(Token, amountSrcDesired, amountCrsMin, amountSrcMin, to, 0ul);

            addLiquidityResponse[0].Should().Be(amountCrsDesired);
            addLiquidityResponse[1].Should().Be(amountSrcDesired);
            // It is not this tests responsibility to validate the returned minted liquidity tokens
            addLiquidityResponse[2].Should().Be(It.IsAny<UInt256>());
            
            VerifyCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, Times.Once);
            VerifyCall(Pool, amountCrsDesired, nameof(IOpdexStandardPool.Mint), mintParams, Times.Once);
        }
        
        [Theory]
        [InlineData(1_000, 1_500, 500, 750, 100_000, 150_000, true)]
        [InlineData(25_000, 75_000, 20_000, 60_000, 2_500_000, 7_500_000, false)]
        public void AddLiquidity_Success_ExistingReserves_TokenOptimal(ulong amountCrsDesired, UInt256 amountSrcDesired, ulong amountCrsMin, 
            UInt256 amountSrcMin, ulong reserveCrs, UInt256 reserveSrc, bool authProvider)
        {
            var sender = Trader0;
            var to = OtherAddress;
            
            var market = CreateNewOpdexStandardMarket();

            State.SetAddress($"Pool:{Token}", Pool);

            if (authProvider)
            {
                State.SetBool($"AuthorizedFor:{(byte)Permissions.Provide}:{sender}", true);
                State.SetBool($"AuthorizedFor:{(byte)Permissions.Provide}:{to}", true);
            }
            
            SetupMessage(Controller, sender, amountCrsDesired);

            // Call to get reserves from pool
            var expectedReserves = new object[] { reserveCrs, reserveSrc };
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));
            
            // Transfer SRC to Pool
            var expectedAmountSrcOptimal = market.GetLiquidityQuote(amountCrsDesired, reserveCrs, reserveSrc);
            var transferFromParams = new object[] {sender, Pool, expectedAmountSrcOptimal};
            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));

            // Mint Liquidity Src
            var mintParams = new object[] {to};
            // It is not this tests responsibility to validate the minted liquidity tokens amounts
            SetupCall(Pool, amountCrsDesired, nameof(IOpdexStandardPool.Mint), mintParams, TransferResult.Transferred(It.IsAny<UInt256>()));
            
            var addLiquidityResponse = market.AddLiquidity(Token, amountSrcDesired, amountCrsMin, amountSrcMin, to, 0ul);

            addLiquidityResponse[0].Should().Be(amountCrsDesired);
            addLiquidityResponse[1].Should().Be(expectedAmountSrcOptimal);
            // It is not this tests responsibility to validate the returned minted liquidity tokens
            addLiquidityResponse[2].Should().Be(It.IsAny<UInt256>());
            
            VerifyCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, Times.Once);
            VerifyCall(Pool, amountCrsDesired, nameof(IOpdexStandardPool.Mint), mintParams, Times.Once);
        }
        
        [Theory]
        [InlineData(1_500, 900, 750, 500, 150_000, 100_000)]
        [InlineData(75_000, 24_000, 60_000, 20_000, 7_500_000, 2_500_000)]
        public void AddLiquidity_Success_ExistingReserves_CrsOptimal(ulong amountCrsDesired, UInt256 amountSrcDesired, ulong amountCrsMin, 
            UInt256 amountSrcMin, ulong reserveCrs, UInt256 reserveSrc)
        {
            var to = OtherAddress;
            
            var market = CreateNewOpdexStandardMarket();

            State.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress, amountCrsDesired);

            // Call to get reserves from pool
            var expectedReserves = new object[] { reserveCrs, reserveSrc };
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));
            
            // Transfer SRC to Pool
            // CrsOptimal route always uses amountSrcDesired
            var transferFromParams = new object[] {OtherAddress, Pool, amountSrcDesired};
            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));
            
            var expectedAmountCrsOptimal = (ulong)market.GetLiquidityQuote(amountSrcDesired, reserveSrc, reserveCrs);
            
            // Mint Liquidity Src
            var mintParams = new object[] {to};
            // It is not this tests responsibility to validate the minted liquidity tokens amounts
            SetupCall(Pool, expectedAmountCrsOptimal, nameof(IOpdexStandardPool.Mint), mintParams, TransferResult.Transferred(It.IsAny<UInt256>()));
            
            // Transfer CRS change back to sender
            var change = amountCrsDesired - expectedAmountCrsOptimal;
            SetupTransfer(OtherAddress, change, TransferResult.Transferred(true));

            var addLiquidityResponse = market.AddLiquidity(Token, amountSrcDesired, amountCrsMin, amountSrcMin, to, 0ul);

            addLiquidityResponse[0].Should().Be(expectedAmountCrsOptimal);
            addLiquidityResponse[1].Should().Be(amountSrcDesired);
            // It is not this tests responsibility to validate the returned minted liquidity tokens
            addLiquidityResponse[2].Should().Be(It.IsAny<UInt256>());
            
            VerifyCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, Times.Once);
            VerifyCall(Pool, expectedAmountCrsOptimal, nameof(IOpdexStandardPool.Mint), mintParams, Times.Once);
            VerifyTransfer(OtherAddress, amountCrsDesired - expectedAmountCrsOptimal, Times.Once);
        }

        [Fact]
        public void AddLiquidity_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var market = CreateNewOpdexStandardMarket();

            market
                .Invoking(c => c.AddLiquidity(Token, 10, 10, 10, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }
        
        [Fact]
        public void AddLiquidity_Throws_UnauthorizedProvider_Sender()
        {
            var sender = Trader0;
            var receiver = OtherAddress;
            var market = CreateNewOpdexStandardMarket(authProviders: true);

            SetupMessage(Pool, sender);
            
            State.SetBool($"AuthorizedFor:{(byte)Permissions.Provide}:{receiver}", true);

            market
                .Invoking(c => c.AddLiquidity(Token, 10, 10, 10, receiver, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        [Fact]
        public void AddLiquidity_Throws_UnauthorizedProvider_Receiver()
        {
            var sender = Trader0;
            var receiver = OtherAddress;
            var market = CreateNewOpdexStandardMarket(authProviders: true);

            SetupMessage(Pool, sender);
            
            State.SetBool($"AuthorizedFor:{(byte)Permissions.Provide}:{sender}", true);

            market
                .Invoking(c => c.AddLiquidity(Token, 10, 10, 10, receiver, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        #endregion

        #region Remove Liquidity

        [Theory]
        [InlineData(100, 1_000, 1_000, true)]
        [InlineData(100, 1_000, 1_000, false)]
        public void RemoveLiquidity_Success(UInt256 liquidity, ulong amountCrsMin, UInt256 amountSrcMin, bool authProvider)
        {
            var sender = Trader0;
            var receiver = OtherAddress;
            var market = CreateNewOpdexStandardMarket();

            State.SetAddress($"Pool:{Token}", Pool);

            if (authProvider)
            {
                State.SetBool($"AuthorizedFor:{(byte)Permissions.Provide}:{sender}", true);
                State.SetBool($"AuthorizedFor:{(byte)Permissions.Provide}:{receiver}", true);
            }

            SetupMessage(Controller, sender);
            
            // Transfer Liquidity tokens to pool
            var transferFromParams = new object[] {sender, Pool, liquidity};
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));
            
            // Burn liquidity tokens
            var burnParams = new object[] {receiver};
            var expectedBurnResponse = new [] { amountCrsMin, amountSrcMin };
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.Burn), burnParams, TransferResult.Transferred(expectedBurnResponse));

            var removeLiquidityResponse = market.RemoveLiquidity(Token, liquidity, amountCrsMin, amountCrsMin, receiver, 0ul);

            removeLiquidityResponse[0].Should().Be(amountCrsMin);
            removeLiquidityResponse[1].Should().Be(amountSrcMin);
            
            VerifyCall(Pool, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, Times.Once);
            VerifyCall(Pool, 0, nameof(IOpdexStandardPool.Burn), burnParams, Times.Once);
        }

        [Fact]
        public void RemoveLiquidity_Throws_InvalidPool()
        {
            var market = CreateNewOpdexStandardMarket();
            
            SetupMessage(Controller, OtherAddress);
            
            market
                .Invoking(c => c.RemoveLiquidity(Token, 100, 1000, 1000, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }
        
        [Fact]
        public void RemoveLiquidity_Throws_InsufficientCrsAmount()
        {
            UInt256 liquidity = 100;
            const ulong amountCrsMin = 1000;
            UInt256 amountSrcMin = 1000;
            var market = CreateNewOpdexStandardMarket();

            State.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Transfer Liquidity tokens to pool
            var transferFromParams = new object[] {OtherAddress, Pool, liquidity};
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));
            
            // Burn liquidity tokens
            var burnParams = new object[] {OtherAddress};
            var expectedAmountCrsMin = amountCrsMin - 1;
            var expectedBurnResponse = new [] { expectedAmountCrsMin, amountSrcMin };
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.Burn), burnParams, TransferResult.Transferred(expectedBurnResponse));
            
            market
                .Invoking(c => c.RemoveLiquidity(Token, liquidity, amountCrsMin, amountSrcMin, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_CRS_AMOUNT");
        }
        
        [Fact]
        public void RemoveLiquidity_Throws_InsufficientSrcAmount()
        {
            UInt256 liquidity = 100;
            const ulong amountCrsMin = 1000;
            UInt256 amountSrcMin = 1000;
            var market = CreateNewOpdexStandardMarket();

            State.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Transfer Liquidity tokens to pool
            var transferFromParams = new object[] {OtherAddress, Pool, liquidity};
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));
            
            // Burn liquidity tokens
            var burnParams = new object[] {OtherAddress};
            var expectedAmountSrcMin = amountSrcMin - 1;
            var expectedBurnResponse = new [] { amountCrsMin, expectedAmountSrcMin };
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.Burn), burnParams, TransferResult.Transferred(expectedBurnResponse));
            
            market
                .Invoking(c => c.RemoveLiquidity(Token, liquidity, amountCrsMin, amountSrcMin, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_SRC_AMOUNT");
        }
        
        [Fact]
        public void RemoveLiquidity_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var market = CreateNewOpdexStandardMarket();

            market
                .Invoking(c => c.RemoveLiquidity(Token, UInt256.MaxValue, ulong.MaxValue, UInt256.MaxValue, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }
        
        [Fact]
        public void RemoveLiquidity_Throws_UnauthorizedProvider_Sender()
        {
            var sender = Trader0;
            var receiver = OtherAddress;
            var market = CreateNewOpdexStandardMarket(authProviders: true);

            SetupMessage(Pool, sender);
            
            State.SetBool($"AuthorizedFor:{(byte)Permissions.Provide}:{receiver}", true);

            market
                .Invoking(c => c.RemoveLiquidity(Token, 10, 10, 10, receiver, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        [Fact]
        public void RemoveLiquidity_Throws_UnauthorizedProvider_Receiver()
        {
            var sender = Trader0;
            var receiver = OtherAddress;
            var market = CreateNewOpdexStandardMarket(authProviders: true);

            SetupMessage(Pool, sender);
            
            State.SetBool($"AuthorizedFor:{(byte)Permissions.Provide}:{sender}", true);

            market
                .Invoking(c => c.RemoveLiquidity(Token, 10, 10, 10, receiver, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        #endregion

        #region Swap Exact CRS for Src

        [Theory]
        [InlineData(6500, 17_000, 200_000, 450_000, true)]
        [InlineData(6500, 17_000, 200_000, 450_000, false)]
        public void SwapExactCrsForSrc_Success(UInt256 amountSrcOutMin, ulong amountCrsIn, UInt256 reserveSrc, ulong reserveCrs, bool requireAuth)
        {
            // Arrange
            var sender = Trader0;
            var market = CreateNewOpdexStandardMarket(authTraders: requireAuth);
            
            State.SetAddress($"Pool:{Token}", Pool);

            if (requireAuth)
            {
                State.SetBool($"AuthorizedFor:{(byte)Permissions.Trade}:{sender}", true);
            }
            
            SetupMessage(Controller, sender, amountCrsIn);
            
            // Call to get reserves from pool
            var expectedReserves = new object[] { reserveCrs, reserveSrc };
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));
            
            // Calculate actual amount out based on the provided input amount of crs - separate tests for accuracy for this method specifically
            var amountOut = market.GetAmountOut(amountCrsIn, reserveCrs, reserveSrc);

            // Call pool to swap
            var swapParams = new object[] {0ul, amountOut, sender, new byte[0]};
            SetupCall(Pool, amountCrsIn, nameof(IOpdexStandardPool.Swap), swapParams, TransferResult.Transferred(true));
            
            // Act
            var response = market.SwapExactCrsForSrc(amountSrcOutMin, Token, sender, 0);
            response.Should().Be(amountOut);
            
            // Assert
            VerifyCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(Pool, amountCrsIn, nameof(IOpdexStandardPool.Swap), swapParams, Times.Once);
        }
        
        [Fact]
        public void SwapExactCrsForSrc_Throws_UnauthorizedTrader()
        {
            var market = CreateNewOpdexStandardMarket(authTraders: true);

            SetupMessage(Pool, Trader0);
            
            market
                .Invoking(c => c.SwapExactCrsForSrc(1000, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }

        [Fact]
        public void SwapExactCrsForSrc_Throws_InvalidPool()
        {
            var market = CreateNewOpdexStandardMarket();
            
            market
                .Invoking(c => c.SwapExactCrsForSrc(1000, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }
        
        [Theory]
        [InlineData(6500, 14625, 200_000, 450_000, true)]
        public void SwapExactCrsForSrc_Throws_InsufficientOutputAmount(UInt256 amountSrcOutMin, ulong amountCrsIn, UInt256 reserveSrc, ulong reserveCrs, bool requireAuth)
        {
            var market = CreateNewOpdexStandardMarket();
            
            State.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress, amountCrsIn);
            
            // Call to get reserves from pool
            var expectedReserves = new object[] { reserveCrs, reserveSrc };
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));
            
            market
                .Invoking(c => c.SwapExactCrsForSrc(amountSrcOutMin, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        }
        
        [Fact]
        public void SwapExactCrsForSrc_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var market = CreateNewOpdexStandardMarket();

            market
                .Invoking(c => c.SwapExactCrsForSrc(10ul, Token, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }

        #endregion

        #region Swap Src for Exact CRS

        [Theory]
        [InlineData(6500, 17_000, 200_000, 450_000, false)]
        [InlineData(6500, 17_000, 200_000, 450_000, true)]
        public void SwapSrcForExactCrs_Success(ulong amountCrsOut, UInt256 amountSrcInMax, UInt256 reserveSrc, ulong reserveCrs, bool requireAuth)
        {
            // Arrange
            var sender = Trader0;
            var market = CreateNewOpdexStandardMarket();
            
            State.SetAddress($"Pool:{Token}", Pool);
            
            if (requireAuth)
            {
                State.SetBool($"AuthorizedFor:{(byte)Permissions.Trade}:{sender}", true);
            }
            
            SetupMessage(Controller, sender);
            
            // Call to get reserves from pool
            var expectedReserves = new object[] { reserveCrs, reserveSrc };
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));

            // Calculate actual amount out based on the provided input amount of crs - separate tests for accuracy for this method specifically
            var amountIn = market.GetAmountIn(amountCrsOut, reserveSrc, reserveCrs);
            
            // Call token to Transfer from caller to Pool
            var transferFromParams = new object[] { sender, Pool, amountIn };
            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));
            
            // Call pool to swap
            var swapParams = new object[] {amountCrsOut, UInt256.MinValue, sender, new byte[0]};
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.Swap), swapParams, TransferResult.Transferred(true));
            
            // Act
            var response = market.SwapSrcForExactCrs(amountCrsOut, amountIn, Token, sender, 0);
            response.Should().Be(amountIn);
            
            // Assert
            VerifyCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, Times.Once);
            VerifyCall(Pool, 0, nameof(IOpdexStandardPool.Swap), swapParams, Times.Once);
        }

        [Fact]
        public void SwapSrcForExactCrs_Throws_InvalidPool()
        {
            var market = CreateNewOpdexStandardMarket();
            
            market
                .Invoking(c => c.SwapSrcForExactCrs(1000, 1000, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }
        
        [Theory]
        [InlineData(6500, 2000, 200_000, 450_000)]
        public void SwapSrcForExactCrs_Throws_ExcessiveInputAmount(ulong amountCrsOut, UInt256 amountSrcInMax, UInt256 reserveSrc, ulong reserveCrs)
        {
            var market = CreateNewOpdexStandardMarket();
            
            State.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Call to get reserves from pool
            var expectedReserves = new object[] { reserveCrs, reserveSrc };
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));
            
            market
                .Invoking(c => c.SwapSrcForExactCrs(amountCrsOut, amountSrcInMax, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXCESSIVE_INPUT_AMOUNT");
        }
        
        [Fact]
        public void SwapSrcForExactCrs_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var market = CreateNewOpdexStandardMarket();

            market
                .Invoking(c => c.SwapSrcForExactCrs(10ul, UInt256.MaxValue, Token, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }
        
        [Fact]
        public void SwapSrcForExactCrs_Throws_UnauthorizedTrader()
        {
            var market = CreateNewOpdexStandardMarket(authTraders: true);

            SetupMessage(Pool, Trader0);
            
            market
                .Invoking(c => c.SwapSrcForExactCrs(10ul, UInt256.MaxValue, Token, Trader0, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        #endregion

        #region Swap Exact Src for CRS

        [Theory]
        [InlineData(8000, 17_000, 200_000, 450_000, false)]
        [InlineData(8000, 17_000, 200_000, 450_000, true)]
        public void SwapExactSrcForCrs_Success(UInt256 amountSrcIn, ulong amountCrsOutMin, UInt256 reserveSrc, ulong reserveCrs, bool requireAuth)
        {
            // Arrange
            var sender = Trader0;
            var market = CreateNewOpdexStandardMarket();
            
            State.SetAddress($"Pool:{Token}", Pool);
            
            if (requireAuth)
            {
                State.SetBool($"AuthorizedFor:{(byte)Permissions.Trade}:{sender}", true);
            }
            
            SetupMessage(Controller, sender);
            
            // Call to get reserves from pool
            var expectedReserves = new object[] { reserveCrs, reserveSrc };
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));

            // Calculate actual amount out based on the provided input amount of crs - separate tests for accuracy for this method specifically
            var amountOut = market.GetAmountOut(amountSrcIn, reserveSrc, reserveCrs);
            
            // Call token to Transfer from caller to Pool
            var transferFromParams = new object[] { sender, Pool, amountSrcIn };
            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));
            
            // Call pool to swap
            var swapParams = new object[] {(ulong)amountOut, UInt256.MinValue, sender, new byte[0]};
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.Swap), swapParams, TransferResult.Transferred(true));
            
            // Act
            var response = market.SwapExactSrcForCrs(amountSrcIn, amountCrsOutMin, Token, sender, 0);
            response.Should().Be((ulong)amountOut);
            
            // Assert
            VerifyCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, Times.Once);
            VerifyCall(Pool, 0, nameof(IOpdexStandardPool.Swap), swapParams, Times.Once);
        }

        [Fact]
        public void SwapExactSrcForCrs_Throws_InvalidPool()
        {
            var market = CreateNewOpdexStandardMarket();
            
            market
                .Invoking(c => c.SwapExactSrcForCrs(1000, 1000, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }
        
        [Theory]
        [InlineData(6500, 20000, 200_000, 450_000)]
        public void SwapExactSrcForCrs_Throws_InsufficientOutputAmount(UInt256 amountSrcIn, ulong amountCrsOutMin, UInt256 reserveSrc, ulong reserveCrs)
        {
            var market = CreateNewOpdexStandardMarket();

            State.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Call to get reserves from pool
            var expectedReserves = new object[] { reserveCrs, reserveSrc };
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));
            
            market
                .Invoking(c => c.SwapExactSrcForCrs(amountSrcIn, amountCrsOutMin, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        }
        
        [Fact]
        public void SwapExactSrcForCrs_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var market = CreateNewOpdexStandardMarket();

            market
                .Invoking(c => c.SwapExactSrcForCrs(10ul, 10ul, Token, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }
        
        [Fact]
        public void SwapExactSrcForCrs_Throws_UnAuthorizedTrader()
        {
            var market = CreateNewOpdexStandardMarket(authTraders: true);

            SetupMessage(Pool, Trader0);
            
            market
                .Invoking(c => c.SwapExactSrcForCrs(10ul, 10ul, Token, Trader0, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        #endregion

        #region Swap CRS for Exact Src

        [Theory]
        [InlineData(24_000, 10_000, 200_000, 450_000, false)]
        [InlineData(24_000, 10_000, 200_000, 450_000, true)]
        public void SwapCrsForExactSrc_Success(ulong amountCrsIn, UInt256 amountSrcOut, UInt256 reserveSrc, ulong reserveCrs, bool requireAuth)
        {
            // Arrange
            var sender = Trader0;
            var market = CreateNewOpdexStandardMarket();
            
            State.SetAddress($"Pool:{Token}", Pool);
            
            if (requireAuth)
            {
                State.SetBool($"AuthorizedFor:{(byte)Permissions.Trade}:{sender}", true);
            }
            
            SetupMessage(Controller, sender, amountCrsIn);
            
            // Call to get reserves from pool
            var expectedReserves = new object[] { reserveCrs, reserveSrc };
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));

            var amountIn = (ulong)market.GetAmountIn(amountSrcOut, reserveCrs, reserveSrc);

            var change = amountCrsIn - amountIn;
            
            // Call pool to swap
            var swapParams = new object[] {0ul, amountSrcOut, sender, new byte[0]};
            SetupCall(Pool, amountIn, nameof(IOpdexStandardPool.Swap), swapParams, TransferResult.Transferred(true));

            if (change > 0)
            {
                SetupTransfer(sender, change, TransferResult.Transferred(true));
            }
            
            // Act
            var response = market.SwapCrsForExactSrc(amountSrcOut, Token, sender, 0);
            response.Should().Be(amountIn);
            
            // Assert
            VerifyCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(Pool, amountIn, nameof(IOpdexStandardPool.Swap), swapParams, Times.Once);

            if (change > 0)
            {
                VerifyTransfer(sender, change, Times.Once);
            }
        }

        [Fact]
        public void SwapCrsForExactSrc_Throws_InvalidPool()
        {
            var market = CreateNewOpdexStandardMarket();
            
            market
                .Invoking(c => c.SwapCrsForExactSrc(1000, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }
        
        [Theory]
        [InlineData(2000, 200_000, 450_000)]
        public void SwapCrsForExactSrc_Throws_ExcessiveInputAmount(UInt256 amountSrcOut, UInt256 reserveSrc, ulong reserveCrs)
        {
            var market = CreateNewOpdexStandardMarket();
            
            State.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress);
            
            var expectedReserves = new object[] { reserveCrs, reserveSrc };
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));
            
            market
                .Invoking(c => c.SwapCrsForExactSrc(amountSrcOut, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXCESSIVE_INPUT_AMOUNT");
        }
        
        [Fact]
        public void SwapCrsForExactSrc_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var market = CreateNewOpdexStandardMarket();

            market
                .Invoking(c => c.SwapCrsForExactSrc(UInt256.MaxValue, Token, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }
        
        [Fact]
        public void SwapCrsForExactSrc_Throws_UnauthorizedTrader()
        {
            var market = CreateNewOpdexStandardMarket(authTraders: true);

            SetupMessage(Pool, Trader0);
            
            market
                .Invoking(c => c.SwapCrsForExactSrc(UInt256.MaxValue, Token, Trader0, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }

        #endregion
        
        #region Swap SRC for Exact SRC Src
        
        [Theory]
        [InlineData(24_000, 107_000, 200_000, 450_000, 200_000, 450_000, true)]
        [InlineData(100_000_000, 1_000_000_000, 10009900000000, 99901400322, 9990079661494, 100099600698, false)]
        public void SwapSrcForExactSrc_Success(UInt256 amountSrcOut, UInt256 amountSrcInMax, 
            UInt256 reserveSrcIn, ulong reserveCrsIn, UInt256 reserveSrcOut, ulong reserveCrsOut, bool requireAuth)
        {
            var tokenIn = Token;
            var tokenOut = TokenTwo;
            var tokenInPool = Pool;
            var tokenOutPool = PoolTwo;
            var sender = Trader0;
            var market = CreateNewOpdexStandardMarket();
            
            State.SetAddress($"Pool:{tokenIn}", tokenInPool);
            State.SetAddress($"Pool:{tokenOut}", tokenOutPool);
            
            if (requireAuth)
            {
                State.SetBool($"AuthorizedFor:{(byte)Permissions.Trade}:{sender}", true);
            }
            
            SetupMessage(Controller, sender);
            
            // Call to get reserves from pool
            var expectedTokenInReserves = new object[] { reserveCrsIn, reserveSrcIn };
            SetupCall(tokenInPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenInReserves));
            
            // Call to get reserves from pool
            var expectedTokenOutReserves = new object[] { reserveCrsOut, reserveSrcOut };
            SetupCall(tokenOutPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenOutReserves));
        
            var amountCrs = (ulong)market.GetAmountIn(amountSrcOut, reserveCrsOut, reserveSrcOut);
            var amountSrcIn = market.GetAmountIn(amountCrs, reserveSrcIn, reserveCrsIn);
            
            // Transfer SRC for CRS
            var transferFromParams = new object[] { sender, tokenInPool, amountSrcIn };
            SetupCall(tokenIn, 0ul, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));
            
            // Call pool to swap src to crs
            var swapSrcToCrsParams = new object[] {amountCrs, UInt256.MinValue, Controller, new byte[0]};
            SetupCall(tokenInPool, 0, nameof(IOpdexStandardPool.Swap), swapSrcToCrsParams, TransferResult.Transferred(true), () => SetupBalance(amountCrs));
            
            // Call pool to swap crs to src
            var swapCrsToSrcParams = new object[] { 0ul, amountSrcOut, sender, new byte[0]};
            SetupCall(tokenOutPool, 0, nameof(IOpdexStandardPool.Swap), swapCrsToSrcParams, TransferResult.Transferred(true));
            
            // Act
            var response = market.SwapSrcForExactSrc(amountSrcInMax, tokenIn, amountSrcOut, tokenOut, sender, 0);
            response.Should().Be(amountSrcIn);
            
            // Assert
            VerifyCall(tokenInPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(tokenOutPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(tokenInPool, 0, nameof(IOpdexStandardPool.Swap), swapSrcToCrsParams, Times.Once);
            VerifyCall(tokenOutPool, 0, nameof(IOpdexStandardPool.Swap), swapCrsToSrcParams, Times.Once);
        }

        [Theory]
        [InlineData(24_000, 10_000, 200_000, 450_000, 200_000, 450_000)]
        public void SwapSrcForExactSrc_Throws_InsufficientInputAmount(UInt256 amountSrcOut, UInt256 amountSrcInMax,
            UInt256 reserveSrcIn, ulong reserveCrsIn, UInt256 reserveSrcOut, ulong reserveCrsOut)
        {
            var tokenIn = Token;
            var tokenOut = TokenTwo;
            var tokenInPool = Pool;
            var tokenOutPool = PoolTwo;
            
            // Arrange
            var market = CreateNewOpdexStandardMarket();
            
            State.SetAddress($"Pool:{tokenIn}", tokenInPool);
            State.SetAddress($"Pool:{tokenOut}", tokenOutPool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Call to get reserves from pool
            var expectedTokenInReserves = new object[] {reserveCrsIn, reserveSrcIn};
            SetupCall(tokenInPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenInReserves));
            
            // Call to get reserves from pool
            var expectedTokenOutReserves = new object[] { reserveCrsOut, reserveSrcOut };
            SetupCall(tokenOutPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenOutReserves));
            
            market
                .Invoking(c => c.SwapSrcForExactSrc(amountSrcInMax, tokenIn, amountSrcOut, tokenOut, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        }
        
        [Fact]
        public void SwapSrcForExactSrc_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var market = CreateNewOpdexStandardMarket();

            market
                .Invoking(c => c.SwapSrcForExactSrc(UInt256.MaxValue, Token, UInt256.MaxValue, TokenTwo, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }
        
        [Fact]
        public void SwapSrcForExactSrc_Throws_UnauthorizedTrader()
        {
            var market = CreateNewOpdexStandardMarket(authTraders: true);

            SetupMessage(Pool, Trader0);
            
            market
                .Invoking(c => c.SwapSrcForExactSrc(UInt256.MaxValue, Token, UInt256.MaxValue, TokenTwo, Trader0, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        #endregion
        
        #region Swap Exact SRC for SRC Src
        
        [Theory]
        [InlineData(24_000, 19_000, 200_000, 450_000, 200_000, 450_000, false)]
        [InlineData(100_000_000_000, 1_000_000, 2_500_000_000_000, 10_000_000_000, 2_500_000_000_000, 10_000_000_000, true)]
        public void SwapExactSrcForSrc_Success(UInt256 amountSrcIn, UInt256 amountSrcOutMin, 
            UInt256 reserveSrcIn, ulong reserveCrsIn, UInt256 reserveSrcOut, ulong reserveCrsOut, bool requireAuth)
        {
            var tokenIn = Token;
            var tokenOut = TokenTwo;
            var tokenInPool = Pool;
            var tokenOutPool = PoolTwo;
            var sender = Trader0;
            var market = CreateNewOpdexStandardMarket();
            
            State.SetAddress($"Pool:{tokenIn}", tokenInPool);
            State.SetAddress($"Pool:{tokenOut}", tokenOutPool);
            
            if (requireAuth)
            {
                State.SetBool($"AuthorizedFor:{(byte)Permissions.Trade}:{sender}", true);
            }
            
            SetupMessage(Controller, sender);
            
            // Call to get reserves from pool
            var expectedTokenInReserves = new object[] { reserveCrsIn, reserveSrcIn };
            SetupCall(tokenInPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenInReserves));
            
            // Call to get reserves from pool
            var expectedTokenOutReserves = new object[] { reserveCrsOut, reserveSrcOut };
            SetupCall(tokenOutPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenOutReserves));
        
            var amountCrsOut = (ulong)market.GetAmountOut(amountSrcIn, reserveSrcOut, reserveCrsOut);
            var amountSrcOut = market.GetAmountOut(amountCrsOut, reserveCrsIn, reserveSrcIn);
            
            // Transfer SRC for CRS
            var transferFromParams = new object[] { sender, tokenInPool, amountSrcIn };
            SetupCall(tokenIn, 0ul, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));
        
            // Call pool to swap src to crs
            var swapSrcToCrsParams = new object[] {amountCrsOut, UInt256.MinValue, Controller, new byte[0]};
            SetupCall(tokenInPool, 0, nameof(IOpdexStandardPool.Swap), swapSrcToCrsParams, TransferResult.Transferred(true), () => SetupBalance(amountCrsOut));
            
            // Call pool to swap crs to src
            var swapCrsToSrcParams = new object[] { 0ul, amountSrcOut, sender, new byte[0]};
            SetupCall(tokenOutPool, 0, nameof(IOpdexStandardPool.Swap), swapCrsToSrcParams, TransferResult.Transferred(true));
            
            // Act
            var response = market.SwapExactSrcForSrc(amountSrcIn, tokenIn, amountSrcOutMin, tokenOut, sender, 0);
            response.Should().Be(amountSrcOut);
            
            // Assert
            VerifyCall(tokenInPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(tokenOutPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(tokenInPool, 0, nameof(IOpdexStandardPool.Swap), swapSrcToCrsParams, Times.Once);
            VerifyCall(tokenOutPool, 0, nameof(IOpdexStandardPool.Swap), swapCrsToSrcParams, Times.Once);
        }

        [Theory]
        [InlineData(24_000, 20_000, 200_000, 450_000, 200_000, 450_000)]
        public void SwapExactSrcForSrc_Throws_InsufficientInputAmount(UInt256 amountSrcIn, UInt256 amountSrcOutMin,
            UInt256 reserveSrcIn, ulong reserveCrsIn, UInt256 reserveSrcOut, ulong reserveCrsOut)
        {
            var tokenIn = Token;
            var tokenOut = TokenTwo;
            var tokenInPool = Pool;
            var tokenOutPool = PoolTwo;
            
            // Arrange
            var market = CreateNewOpdexStandardMarket();
            
            State.SetAddress($"Pool:{tokenIn}", tokenInPool);
            State.SetAddress($"Pool:{tokenOut}", tokenOutPool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Call to get reserves from pool
            var expectedTokenInReserves = new object[] { reserveCrsIn, reserveSrcIn };
            SetupCall(tokenInPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenInReserves));
            
            // Call to get reserves from pool
            var expectedTokenOutReserves = new object[] { reserveCrsOut, reserveSrcOut };
            SetupCall(tokenOutPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenOutReserves));
            
            market
                .Invoking(c => c.SwapExactSrcForSrc(amountSrcIn, tokenIn, amountSrcOutMin, tokenOut, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        }
        
        [Fact]
        public void SwapExactSrcForSrc_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var market = CreateNewOpdexStandardMarket();

            market
                .Invoking(c => c.SwapExactSrcForSrc(UInt256.MaxValue, Token, UInt256.MaxValue, TokenTwo, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }
        
        [Fact]
        public void SwapExactSrcForSrc_Throws_UnauthorizedTrader()
        {
            var market = CreateNewOpdexStandardMarket(authTraders: true);
            
            SetupMessage(Pool, Trader0);
            
            market
                .Invoking(c => c.SwapExactSrcForSrc(UInt256.MaxValue, Token, UInt256.MaxValue, TokenTwo, Trader0, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: UNAUTHORIZED");
        }
        
        #endregion

        # region Public Helpers

        [Theory]
        [InlineData(1, 2, 3)]
        [InlineData(243, 345, 847)]
        // Todo: Precalculate expected results
        // Todo: Add more scenarios with precalculated expected results
        public void GetLiquidityQuote_Success(UInt256 amountA, UInt256 reserveA, UInt256 reserveB)
        {
            var market = CreateNewOpdexStandardMarket();
            var expected = amountA * reserveB / reserveA;
            var quote = market.GetLiquidityQuote(amountA, reserveA, reserveB);

            quote.Should().Be(expected);
        }

        [Fact]
        public void GetLiquidityQuote_Throws_InsufficientAmount()
        {
            var market = CreateNewOpdexStandardMarket();
            
            market
                .Invoking(c => c.GetLiquidityQuote(0, 10, 100))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_AMOUNT");
        }
        
        [Theory]
        [InlineData(10, 1000, 0)]
        [InlineData(10, 0, 1000)]
        public void GetLiquidityQuote_Throws_InsufficientLiquidity(UInt256 amountA, UInt256 reserveA, UInt256 reserveB)
        {
            var market = CreateNewOpdexStandardMarket();
            
            market
                .Invoking(c => c.GetLiquidityQuote(amountA, reserveA, reserveB))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }

        [Theory]
        [InlineData(1_000, 10_000, 100_000, 9_066)]
        // Todo: Add more scenarios with precalculated expected results
        public void GetAmountOut_Success(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut, UInt256 expected)
        {
            var market = CreateNewOpdexStandardMarket();

            var amountOut = market.GetAmountOut(amountIn, reserveIn, reserveOut);

            amountOut.Should().Be(expected);
        }
        
        [Theory]
        [InlineData(0, 10_000, 100_000)]
        public void GetAmountOut_Throws_InsufficientInputAmount(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut)
        {
            var market = CreateNewOpdexStandardMarket();

            market
                .Invoking(c => c.GetAmountOut(amountIn, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        }
        
        [Theory]
        [InlineData(1_000, 0, 100_000)]
        [InlineData(1_000, 10_000, 0)]
        public void GetAmountOut_Throws_InsufficientLiquidity(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut)
        {
            var market = CreateNewOpdexStandardMarket();

            market
                .Invoking(c => c.GetAmountOut(amountIn, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }
        
        [Theory]
        [InlineData(10_000, 10_000, 100_000, 1_115)]
        // Todo: Add more scenarios with precalculated expected results
        public void GetAmountIn_Success(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut, UInt256 expected)
        {
            var market = CreateNewOpdexStandardMarket();

            var amountIn = market.GetAmountIn(amountOut, reserveIn, reserveOut);

            amountIn.Should().Be(expected);
        }
        
        [Theory]
        [InlineData(0, 10_000, 100_000)]
        public void GetAmountIn_Throws_InsufficientInputAmount(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut)
        {
            var market = CreateNewOpdexStandardMarket();

            market
                .Invoking(c => c.GetAmountIn(amountOut, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        }
        
        [Theory]
        [InlineData(1_000, 0, 100_000)]
        [InlineData(1_000, 10_000, 0)]
        public void GetAmountIn_Throws_InsufficientLiquidity(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut)
        {
            var market = CreateNewOpdexStandardMarket();

            market
                .Invoking(c => c.GetAmountIn(amountOut, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }
        
        #endregion
    }
}
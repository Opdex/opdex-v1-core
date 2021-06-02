using FluentAssertions;
using Moq;
using OpdexV1Core.Tests.Base;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexV1Core.Tests.Routers
{
    public class OpdexRouterTests : TestBase
    {
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

            var market = CreateNewOpdexRouter(StakingMarket, 3);

            State.SetAddress($"Pool:{Token}", Pool);

            SetupMessage(StakingMarket, OtherAddress, amountCrsDesired);

            // Call to get reserves from pool
            var expectedReserves = new[] {expectedReserveCrs, expectedReserveSrc};
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));

            // Transfer SRC to Pool
            var transferFromParams = new object[] {OtherAddress, Pool, amountSrcDesired};
            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));

            // Mint Liquidity Src
            var mintParams = new object[] {to};
            // It is not this tests responsibility to validate the minted liquidity tokens amounts
            SetupCall(Pool, amountCrsDesired, nameof(IOpdexStandardPool.Mint), mintParams, TransferResult.Transferred(It.IsAny<UInt256>()));

            var addLiquidityResponse = market.AddLiquidity(Token, amountSrcDesired, amountCrsMin, amountSrcMin, to, 0ul);

            addLiquidityResponse[0].Should().Be((UInt256) amountCrsDesired);
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

            var market = CreateNewOpdexRouter(StakingMarket, 3);

            State.SetAddress($"Pool:{Token}", Pool);

            if (authProvider)
            {
                State.SetBool($"IsAuthorized:{(byte) Permissions.Provide}:{sender}", true);
                State.SetBool($"IsAuthorized:{(byte) Permissions.Provide}:{to}", true);
            }

            SetupMessage(StakingMarket, sender, amountCrsDesired);

            // Call to get reserves from pool
            var expectedReserves = new[] {reserveCrs, reserveSrc};
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

            addLiquidityResponse[0].Should().Be((UInt256) amountCrsDesired);
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

            var market = CreateNewOpdexRouter(StakingMarket, 3);

            State.SetAddress($"Pool:{Token}", Pool);

            SetupMessage(StakingMarket, OtherAddress, amountCrsDesired);

            // Call to get reserves from pool
            var expectedReserves = new[] {reserveCrs, reserveSrc};
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));

            // Transfer SRC to Pool
            // CrsOptimal route always uses amountSrcDesired
            var transferFromParams = new object[] {OtherAddress, Pool, amountSrcDesired};
            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));

            var expectedAmountCrsOptimal = (ulong) market.GetLiquidityQuote(amountSrcDesired, reserveSrc, reserveCrs);

            // Mint Liquidity Src
            var mintParams = new object[] {to};
            // It is not this tests responsibility to validate the minted liquidity tokens amounts
            SetupCall(Pool, expectedAmountCrsOptimal, nameof(IOpdexStandardPool.Mint), mintParams, TransferResult.Transferred(It.IsAny<UInt256>()));

            // Transfer CRS change back to sender
            var change = amountCrsDesired - expectedAmountCrsOptimal;
            SetupTransfer(OtherAddress, change, TransferResult.Transferred(true));

            var addLiquidityResponse = market.AddLiquidity(Token, amountSrcDesired, amountCrsMin, amountSrcMin, to, 0ul);

            addLiquidityResponse[0].Should().Be((UInt256) expectedAmountCrsOptimal);
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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.AddLiquidity(Token, 10, 10, 10, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            State.SetAddress($"Pool:{Token}", Pool);

            if (authProvider)
            {
                State.SetBool($"IsAuthorized:{(byte) Permissions.Provide}:{sender}", true);
                State.SetBool($"IsAuthorized:{(byte) Permissions.Provide}:{receiver}", true);
            }

            SetupMessage(StakingMarket, sender);

            // Transfer Liquidity tokens to pool
            var transferFromParams = new object[] {sender, Pool, liquidity};
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));

            // Burn liquidity tokens
            var burnParams = new object[] {receiver};
            var expectedBurnResponse = new[] {amountCrsMin, amountSrcMin};
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.Burn), burnParams, TransferResult.Transferred(expectedBurnResponse));

            var removeLiquidityResponse = market.RemoveLiquidity(Token, liquidity, amountCrsMin, amountCrsMin, receiver, 0ul);

            removeLiquidityResponse[0].Should().Be((UInt256) amountCrsMin);
            removeLiquidityResponse[1].Should().Be(amountSrcMin);

            VerifyCall(Pool, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, Times.Once);
            VerifyCall(Pool, 0, nameof(IOpdexStandardPool.Burn), burnParams, Times.Once);
        }

        [Fact]
        public void RemoveLiquidity_Throws_InvalidPool()
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            SetupCall(StakingMarket, 0, nameof(IOpdexStakingMarket.GetPool), new object[] {Token}, TransferResult.Transferred(Address.Zero));
            
            SetupMessage(StakingMarket, OtherAddress);

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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            State.SetAddress($"Pool:{Token}", Pool);

            SetupMessage(StakingMarket, OtherAddress);

            // Transfer Liquidity tokens to pool
            var transferFromParams = new object[] {OtherAddress, Pool, liquidity};
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));

            // Burn liquidity tokens
            var burnParams = new object[] {OtherAddress};
            var expectedAmountCrsMin = amountCrsMin - 1;
            var expectedBurnResponse = new[] {expectedAmountCrsMin, amountSrcMin};
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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            State.SetAddress($"Pool:{Token}", Pool);

            SetupMessage(StakingMarket, OtherAddress);

            // Transfer Liquidity tokens to pool
            var transferFromParams = new object[] {OtherAddress, Pool, liquidity};
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));

            // Burn liquidity tokens
            var burnParams = new object[] {OtherAddress};
            var expectedAmountSrcMin = amountSrcMin - 1;
            var expectedBurnResponse = new[] {amountCrsMin, expectedAmountSrcMin};
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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.RemoveLiquidity(Token, UInt256.MaxValue, ulong.MaxValue, UInt256.MaxValue, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }

        #endregion

        #region Swap Exact CRS for Src

        [Theory]
        [InlineData(7280, 17_000, 200_000, 450_000, true, 0)]
        [InlineData(7273, 17_000, 200_000, 450_000, false, 1)]
        [InlineData(7266, 17_000, 200_000, 450_000, true, 2)]
        [InlineData(7259, 17_000, 200_000, 450_000, false, 3)]
        [InlineData(7252, 17_000, 200_000, 450_000, true, 4)]
        [InlineData(7245, 17_000, 200_000, 450_000, false, 5)]
        [InlineData(7238, 17_000, 200_000, 450_000, true, 6)]
        [InlineData(7231, 17_000, 200_000, 450_000, false, 7)]
        [InlineData(7224, 17_000, 200_000, 450_000, true, 8)]
        [InlineData(7217, 17_000, 200_000, 450_000, false, 9)]
        [InlineData(7210, 17_000, 200_000, 450_000, true, 10)]
        public void SwapExactCrsForSrc_Success(UInt256 amountSrcOutMin, ulong amountCrsIn, UInt256 reserveSrc, ulong reserveCrs, bool requireAuth, uint fee)
        {
            var sender = Trader0;
            var market = CreateNewOpdexRouter(StakingMarket, fee);

            State.SetAddress($"Pool:{Token}", Pool);

            if (requireAuth)
            {
                State.SetBool($"IsAuthorized:{(byte) Permissions.Trade}:{sender}", true);
            }

            SetupMessage(StakingMarket, sender, amountCrsIn);

            // Call to get reserves from pool
            var expectedReserves = new[] {reserveCrs, reserveSrc};
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
        public void SwapExactCrsForSrc_Throws_InvalidPool()
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);
            
            SetupCall(StakingMarket, 0, nameof(IOpdexStakingMarket.GetPool), new object[] {Token}, TransferResult.Transferred(Address.Zero));

            market
                .Invoking(c => c.SwapExactCrsForSrc(1000, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }

        [Theory]
        [InlineData(6500, 14625, 200_000, 450_000)]
        public void SwapExactCrsForSrc_Throws_InsufficientOutputAmount(UInt256 amountSrcOutMin, ulong amountCrsIn, UInt256 reserveSrc, ulong reserveCrs)
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            State.SetAddress($"Pool:{Token}", Pool);

            SetupMessage(StakingMarket, OtherAddress, amountCrsIn);

            // Call to get reserves from pool
            var expectedReserves = new[] {reserveCrs, reserveSrc};
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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.SwapExactCrsForSrc(10ul, Token, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }

        #endregion

        #region Swap Src for Exact CRS

        [Theory]
        [InlineData(6500, 2932, 200_000, 450_000, false, 0)]
        [InlineData(6500, 2935, 200_000, 450_000, true, 1)]
        [InlineData(6500, 2938, 200_000, 450_000, false, 2)]
        [InlineData(6500, 2941, 200_000, 450_000, true, 3)]
        [InlineData(6500, 2944, 200_000, 450_000, false, 4)]
        [InlineData(6500, 2946, 200_000, 450_000, true, 5)]
        [InlineData(6500, 2949, 200_000, 450_000, false, 6)]
        [InlineData(6500, 2952, 200_000, 450_000, true, 7)]
        [InlineData(6500, 2955, 200_000, 450_000, false, 8)]
        [InlineData(6500, 2958, 200_000, 450_000, true, 9)]
        [InlineData(6500, 2961, 200_000, 450_000, false, 10)]
        public void SwapSrcForExactCrs_Success(ulong amountCrsOut, UInt256 expectedSrcIn, UInt256 reserveSrc, ulong reserveCrs, bool requireAuth, uint fee)
        {
            var sender = Trader0;
            var market = CreateNewOpdexRouter(StakingMarket, fee);

            State.SetAddress($"Pool:{Token}", Pool);

            if (requireAuth)
            {
                State.SetBool($"IsAuthorized:{(byte) Permissions.Trade}:{sender}", true);
            }

            SetupMessage(StakingMarket, sender);

            // Call to get reserves from pool
            var expectedReserves = new[] {reserveCrs, reserveSrc};
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));

            // Call token to Transfer from caller to Pool
            var transferFromParams = new object[] {sender, Pool, expectedSrcIn};
            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));

            // Call pool to swap
            var swapParams = new object[] {amountCrsOut, UInt256.MinValue, sender, new byte[0]};
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.Swap), swapParams, TransferResult.Transferred(true));

            // Act
            var response = market.SwapSrcForExactCrs(amountCrsOut, expectedSrcIn, Token, sender, 0);
            response.Should().Be(expectedSrcIn);

            // Assert
            VerifyCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, Times.Once);
            VerifyCall(Pool, 0, nameof(IOpdexStandardPool.Swap), swapParams, Times.Once);
        }

        [Fact]
        public void SwapSrcForExactCrs_Throws_InvalidPool()
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            SetupCall(StakingMarket, 0, nameof(IOpdexStakingMarket.GetPool), new object[] {Token}, TransferResult.Transferred(Address.Zero));
            
            market
                .Invoking(c => c.SwapSrcForExactCrs(1000, 1000, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }

        [Theory]
        [InlineData(ulong.MaxValue - 1, 2000, 200_000, ulong.MaxValue)]
        public void SwapSrcForExactCrs_Throws_ExcessiveInputAmount(ulong amountCrsOut, UInt256 amountSrcInMax, UInt256 reserveSrc, ulong reserveCrs)
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            State.SetAddress($"Pool:{Token}", Pool);

            SetupMessage(StakingMarket, OtherAddress);

            // Call to get reserves from pool
            var expectedReserves = new[] {reserveCrs, reserveSrc};
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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.SwapSrcForExactCrs(10ul, UInt256.MaxValue, Token, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }

        #endregion

        #region Swap Exact Src for CRS

        [Theory]
        [InlineData(8000, 17307, 200_000, 450_000, true, 0)]
        [InlineData(8000, 17291, 200_000, 450_000, false, 1)]
        [InlineData(8000, 17274, 200_000, 450_000, true, 2)]
        [InlineData(8000, 17257, 200_000, 450_000, false, 3)]
        [InlineData(8000, 17241, 200_000, 450_000, true, 4)]
        [InlineData(8000, 17224, 200_000, 450_000, false, 5)]
        [InlineData(8000, 17207, 200_000, 450_000, true, 6)]
        [InlineData(8000, 17191, 200_000, 450_000, false, 7)]
        [InlineData(8000, 17174, 200_000, 450_000, true, 8)]
        [InlineData(8000, 17157, 200_000, 450_000, false, 9)]
        [InlineData(8000, 17141, 200_000, 450_000, true, 10)]
        public void SwapExactSrcForCrs_Success(UInt256 amountSrcIn, ulong amountCrsOutMin, UInt256 reserveSrc, ulong reserveCrs, bool requireAuth, uint fee)
        {
            var sender = Trader0;
            var market = CreateNewOpdexRouter(StakingMarket, fee);

            State.SetAddress($"Pool:{Token}", Pool);

            if (requireAuth)
            {
                State.SetBool($"IsAuthorized:{(byte) Permissions.Trade}:{sender}", true);
            }

            SetupMessage(StakingMarket, sender);

            // Call to get reserves from pool
            var expectedReserves = new[] {reserveCrs, reserveSrc};
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));

            // Calculate actual amount out based on the provided input amount of crs - separate tests for accuracy for this method specifically
            var amountOut = market.GetAmountOut(amountSrcIn, reserveSrc, reserveCrs);

            // Call token to Transfer from caller to Pool
            var transferFromParams = new object[] {sender, Pool, amountSrcIn};
            SetupCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));

            // Call pool to swap
            var swapParams = new object[] {(ulong) amountOut, UInt256.MinValue, sender, new byte[0]};
            SetupCall(Pool, 0, nameof(IOpdexStandardPool.Swap), swapParams, TransferResult.Transferred(true));

            // Act
            var response = market.SwapExactSrcForCrs(amountSrcIn, amountCrsOutMin, Token, sender, 0);
            response.Should().Be((ulong) amountOut);

            // Assert
            VerifyCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(Token, 0, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, Times.Once);
            VerifyCall(Pool, 0, nameof(IOpdexStandardPool.Swap), swapParams, Times.Once);
        }

        [Fact]
        public void SwapExactSrcForCrs_Throws_InvalidPool()
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);
            
            SetupCall(StakingMarket, 0, nameof(IOpdexStakingMarket.GetPool), new object[] {Token}, TransferResult.Transferred(Address.Zero));

            market
                .Invoking(c => c.SwapExactSrcForCrs(1000, 1000, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }

        [Theory]
        [InlineData(6500, 20000, 200_000, 450_000)]
        public void SwapExactSrcForCrs_Throws_InsufficientOutputAmount(UInt256 amountSrcIn, ulong amountCrsOutMin, UInt256 reserveSrc, ulong reserveCrs)
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            State.SetAddress($"Pool:{Token}", Pool);

            SetupMessage(StakingMarket, OtherAddress);

            // Call to get reserves from pool
            var expectedReserves = new[] {reserveCrs, reserveSrc};
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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.SwapExactSrcForCrs(10ul, 10ul, Token, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }

        #endregion

        #region Swap CRS for Exact Src

        [Theory]
        [InlineData(23_685, 10_000, 200_000, 450_000, false, 0)]
        [InlineData(23_708, 10_000, 200_000, 450_000, true, 1)]
        [InlineData(23_732, 10_000, 200_000, 450_000, false, 2)]
        [InlineData(23_756, 10_000, 200_000, 450_000, true, 3)]
        [InlineData(23_780, 10_000, 200_000, 450_000, false, 4)]
        [InlineData(23_804, 10_000, 200_000, 450_000, true, 5)]
        [InlineData(23_828, 10_000, 200_000, 450_000, false, 6)]
        [InlineData(23_852, 10_000, 200_000, 450_000, true, 7)]
        [InlineData(23_876, 10_000, 200_000, 450_000, false, 8)]
        [InlineData(23_900, 10_000, 200_000, 450_000, true, 9)]
        [InlineData(23_924, 10_000, 200_000, 450_000, true, 10)]
        [InlineData(100_000, 10_000, 200_000, 450_000, true, 10)]
        public void SwapCrsForExactSrc_Success(ulong amountCrsIn, UInt256 amountSrcOut, UInt256 reserveSrc, ulong reserveCrs, bool requireAuth, uint fee)
        {
            var sender = Trader0;
            var market = CreateNewOpdexRouter(StakingMarket, fee);

            State.SetAddress($"Pool:{Token}", Pool);

            if (requireAuth)
            {
                State.SetBool($"IsAuthorized:{(byte) Permissions.Trade}:{sender}", true);
            }

            SetupMessage(StakingMarket, sender, amountCrsIn);

            // Call to get reserves from pool
            var expectedReserves = new[] {reserveCrs, reserveSrc};
            SetupCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedReserves));

            var expectedAmountIn = (ulong) market.GetAmountIn(amountSrcOut, reserveCrs, reserveSrc);

            ulong change = amountCrsIn - expectedAmountIn;

            if (change > 0)
            {
                SetupTransfer(sender, change, TransferResult.Transferred(true));
                amountCrsIn = expectedAmountIn;
            }

            // Call pool to swap
            var swapParams = new object[] {0ul, amountSrcOut, sender, new byte[0]};
            SetupCall(Pool, amountCrsIn, nameof(IOpdexStandardPool.Swap), swapParams, TransferResult.Transferred(true));

            // Act
            var response = market.SwapCrsForExactSrc(amountSrcOut, Token, sender, 0);
            response.Should().Be(amountCrsIn).And.Be(expectedAmountIn);

            // Assert
            VerifyCall(Pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, Times.Once);
            VerifyCall(Pool, amountCrsIn, nameof(IOpdexStandardPool.Swap), swapParams, Times.Once);

            if (change > 0)
            {
                VerifyTransfer(sender, change, Times.Once);
            }
        }

        [Fact]
        public void SwapCrsForExactSrc_Throws_InvalidPool()
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            SetupCall(StakingMarket, 0, nameof(IOpdexStakingMarket.GetPool), new object[] {Token}, TransferResult.Failed());

            market
                .Invoking(c => c.SwapCrsForExactSrc(1000, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }

        [Theory]
        [InlineData(2000, 200_000, 450_000)]
        public void SwapCrsForExactSrc_Throws_ExcessiveInputAmount(UInt256 amountSrcOut, UInt256 reserveSrc, ulong reserveCrs)
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            State.SetAddress($"Pool:{Token}", Pool);

            SetupMessage(StakingMarket, OtherAddress);

            var expectedReserves = new[] {reserveCrs, reserveSrc};
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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.SwapCrsForExactSrc(UInt256.MaxValue, Token, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }

        #endregion

        #region Swap SRC for Exact SRC Src

        [Theory]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 3760, 7532, 0, false)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 3764, 7547, 1, true)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 3767, 7561, 2, false)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 3771, 7577, 3, true)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 3775, 7592, 4, false)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 3779, 7608, 5, true)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 3783, 7624, 6, false)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 3786, 7637, 7, true)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 3790, 7653, 8, false)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 3794, 7669, 9, true)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 3798, 7685, 10, false)]
        public void SwapSrcForExactSrc_Success(UInt256 tokenOutAmount, UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc,
            UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc, UInt256 expectedCrsInAmount, UInt256 expectedSrcInAmount, uint fee, bool requireAuth)
        {
            var tokenIn = Token;
            var tokenOut = TokenTwo;
            var tokenInPool = Pool;
            var tokenOutPool = PoolTwo;
            var sender = Trader0;
            var market = CreateNewOpdexRouter(StakingMarket, fee);

            State.SetAddress($"Pool:{tokenIn}", tokenInPool);
            State.SetAddress($"Pool:{tokenOut}", tokenOutPool);

            if (requireAuth)
            {
                State.SetBool($"IsAuthorized:{(byte) Permissions.Trade}:{sender}", true);
            }

            SetupMessage(StakingMarket, sender);

            // Call to get reserves from pool
            var expectedTokenInReserves = new[] {tokenInReserveCrs, tokenInReserveSrc};
            SetupCall(tokenInPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenInReserves));

            // Call to get reserves from pool
            var expectedTokenOutReserves = new[] {tokenOutReserveCrs, tokenOutReserveSrc};
            SetupCall(tokenOutPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenOutReserves));

            var amountCrs = (ulong) market.GetAmountIn(tokenOutAmount, tokenOutReserveCrs, tokenOutReserveSrc);
            var amountSrcIn = market.GetAmountIn(amountCrs, tokenInReserveSrc, tokenInReserveCrs);

            // Transfer SRC for CRS
            var transferFromParams = new object[] {sender, tokenInPool, amountSrcIn};
            SetupCall(tokenIn, 0ul, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));

            // Call pool to swap src to crs
            var swapSrcToCrsParams = new object[] {expectedCrsInAmount, UInt256.Zero, tokenOutPool, new byte[0]};
            SetupCall(tokenInPool, 0, nameof(IOpdexStandardPool.Swap), swapSrcToCrsParams, TransferResult.Transferred(true), () => SetupBalance(amountCrs));

            // Call pool to swap crs to src
            var swapCrsToSrcParams = new object[] {0ul, tokenOutAmount, sender, new byte[0]};
            SetupCall(tokenOutPool, 0, nameof(IOpdexStandardPool.Swap), swapCrsToSrcParams, TransferResult.Transferred(true));

            var response = market.SwapSrcForExactSrc(UInt256.MaxValue, tokenIn, tokenOutAmount, tokenOut, sender, 0);
            response.Should().Be(expectedSrcInAmount);

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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            State.SetAddress($"Pool:{tokenIn}", tokenInPool);
            State.SetAddress($"Pool:{tokenOut}", tokenOutPool);

            SetupMessage(StakingMarket, OtherAddress);

            // Call to get reserves from pool
            var expectedTokenInReserves = new[] {reserveCrsIn, reserveSrcIn};
            SetupCall(tokenInPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenInReserves));

            // Call to get reserves from pool
            var expectedTokenOutReserves = new[] {reserveCrsOut, reserveSrcOut};
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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.SwapSrcForExactSrc(UInt256.MaxValue, Token, UInt256.MaxValue, TokenTwo, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }

        #endregion

        #region Swap Exact SRC for SRC Src

        [Theory]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_468, 0, true)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_453, 1, false)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_439, 2, true)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_424, 3, false)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_409, 4, true)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_393, 5, false)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_380, 6, true)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_365, 7, false)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_349, 8, true)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_336, 9, false)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_321, 10, true)]
        public void SwapExactSrcForSrc_Success(UInt256 tokenInAmount, UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc,
            UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc, UInt256 expectedTokenOutAmount, uint fee, bool requireAuth)
        {
            var tokenIn = Token;
            var tokenOut = TokenTwo;
            var tokenInPool = Pool;
            var tokenOutPool = PoolTwo;
            var sender = Trader0;
            var market = CreateNewOpdexRouter(StakingMarket, fee);

            State.SetAddress($"Pool:{tokenIn}", tokenInPool);
            State.SetAddress($"Pool:{tokenOut}", tokenOutPool);

            if (requireAuth)
            {
                State.SetBool($"IsAuthorized:{(byte) Permissions.Trade}:{sender}", true);
            }

            SetupMessage(StakingMarket, sender);

            // Call to get reserves from pool
            var expectedTokenInReserves = new[] {tokenInReserveCrs, tokenInReserveSrc};
            SetupCall(tokenInPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenInReserves));

            // Call to get reserves from pool
            var expectedTokenOutReserves = new[] {tokenOutReserveCrs, tokenOutReserveSrc};
            SetupCall(tokenOutPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenOutReserves));

            var amountCrsOut = (ulong) market.GetAmountOut(tokenInAmount, tokenInReserveSrc, tokenInReserveCrs);

            // Transfer SRC for CRS
            var transferFromParams = new object[] {sender, tokenInPool, tokenInAmount};
            SetupCall(tokenIn, 0ul, nameof(IOpdexStandardPool.TransferFrom), transferFromParams, TransferResult.Transferred(true));

            // Call pool to swap src to crs
            var swapSrcToCrsParams = new object[] {amountCrsOut, UInt256.Zero, tokenOutPool, new byte[0]};
            SetupCall(tokenInPool, 0, nameof(IOpdexStandardPool.Swap), swapSrcToCrsParams, TransferResult.Transferred(true), () => SetupBalance(amountCrsOut));

            // Call pool to swap crs to src
            var swapCrsToSrcParams = new object[] {0ul, expectedTokenOutAmount, sender, new byte[0]};
            SetupCall(tokenOutPool, 0, nameof(IOpdexStandardPool.Swap), swapCrsToSrcParams, TransferResult.Transferred(true));

            // Act
            var response = market.SwapExactSrcForSrc(tokenInAmount, tokenIn, UInt256.Zero, tokenOut, sender, 0);
            response.Should().Be(expectedTokenOutAmount);

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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            State.SetAddress($"Pool:{tokenIn}", tokenInPool);
            State.SetAddress($"Pool:{tokenOut}", tokenOutPool);

            SetupMessage(StakingMarket, OtherAddress);

            // Call to get reserves from pool
            var expectedTokenInReserves = new[] {reserveCrsIn, reserveSrcIn};
            SetupCall(tokenInPool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}", null, TransferResult.Transferred(expectedTokenInReserves));

            // Call to get reserves from pool
            var expectedTokenOutReserves = new[] {reserveCrsOut, reserveSrcOut};
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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.SwapExactSrcForSrc(UInt256.MaxValue, Token, UInt256.MaxValue, TokenTwo, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }

        #endregion

        # region Public Helpers

        [Theory]
        [InlineData(1, 2, 3, 1)]
        [InlineData(10, 50, 25, 5)]
        [InlineData(10, 3, 25, 83)]
        // expected = amountA * reserveB / reserveA;
        public void GetLiquidityQuote_Success(UInt256 amountA, UInt256 reserveA, UInt256 reserveB, UInt256 expected)
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);
            var quote = market.GetLiquidityQuote(amountA, reserveA, reserveB);

            quote.Should().Be(expected);
        }

        [Fact]
        public void GetLiquidityQuote_Throws_InsufficientAmount()
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.GetLiquidityQuote(amountA, reserveA, reserveB))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }

        [Theory]
        [InlineData(100, 1_000, 10_000, 909, 0)]
        [InlineData(100, 1_000, 10_000, 908, 1)]
        [InlineData(100, 1_000, 10_000, 907, 2)]
        [InlineData(100, 1_000, 10_000, 906, 3)]
        [InlineData(100, 1_000, 10_000, 905, 4)]
        [InlineData(100, 1_000, 10_000, 904, 5)]
        [InlineData(100, 1_000, 10_000, 904, 6)]
        [InlineData(100, 1_000, 10_000, 903, 7)]
        [InlineData(100, 1_000, 10_000, 902, 8)]
        [InlineData(100, 1_000, 10_000, 901, 9)]
        [InlineData(100, 1_000, 10_000, 900, 10)]
        [InlineData(500, 2_500, 5_000, 833, 0)]
        [InlineData(500, 2_500, 5_000, 832, 1)]
        [InlineData(500, 2_500, 5_000, 831, 2)]
        [InlineData(500, 2_500, 5_000, 831, 3)]
        [InlineData(500, 2_500, 5_000, 830, 4)]
        [InlineData(500, 2_500, 5_000, 829, 5)]
        [InlineData(500, 2_500, 5_000, 829, 6)]
        [InlineData(500, 2_500, 5_000, 828, 7)]
        [InlineData(500, 2_500, 5_000, 827, 8)]
        [InlineData(500, 2_500, 5_000, 827, 9)]
        [InlineData(500, 2_500, 5_000, 826, 10)]
        // expected = (amountIn * (1000 - Fee) * reserveOut) / (reserveIn * 1000 + (amountIn * (1000 - Fee)))
        public void GetAmountOut_Success(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut, UInt256 expected, uint fee)
        {
            var market = CreateNewOpdexRouter(StakingMarket, fee);

            var amountOut = market.GetAmountOut(amountIn, reserveIn, reserveOut);

            amountOut.Should().Be(expected);
        }

        [Theory]
        [InlineData(0, 10_000, 100_000)]
        public void GetAmountOut_Throws_InsufficientInputAmount(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut)
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.GetAmountOut(amountIn, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }

        [Theory]
        [InlineData(2_000, 1_000, 10_000, 251, 0)]
        [InlineData(2_000, 1_000, 10_000, 251, 1)]
        [InlineData(2_000, 1_000, 10_000, 251, 2)]
        [InlineData(2_000, 1_000, 10_000, 251, 3)]
        [InlineData(2_000, 1_000, 10_000, 252, 4)]
        [InlineData(2_000, 1_000, 10_000, 252, 5)]
        [InlineData(2_000, 1_000, 10_000, 252, 6)]
        [InlineData(2_000, 1_000, 10_000, 252, 7)]
        [InlineData(2_000, 1_000, 10_000, 253, 8)]
        [InlineData(2_000, 1_000, 10_000, 253, 9)]
        [InlineData(2_000, 1_000, 10_000, 253, 10)]
        [InlineData(50_000, 250_000, 500_000, 27_778, 0)]
        [InlineData(50_000, 250_000, 500_000, 27_806, 1)]
        [InlineData(50_000, 250_000, 500_000, 27_834, 2)]
        [InlineData(50_000, 250_000, 500_000, 27_862, 3)]
        [InlineData(50_000, 250_000, 500_000, 27_890, 4)]
        [InlineData(50_000, 250_000, 500_000, 27_918, 5)]
        [InlineData(50_000, 250_000, 500_000, 27_946, 6)]
        [InlineData(50_000, 250_000, 500_000, 27_974, 7)]
        [InlineData(50_000, 250_000, 500_000, 28_002, 8)]
        [InlineData(50_000, 250_000, 500_000, 28_031, 9)]
        [InlineData(50_000, 250_000, 500_000, 28_059, 10)]
        // expected = (reserveIn * amountOut * 1000) / ((reserveOut - amountOut) * (1000 - Fee)) + 1
        public void GetAmountIn_Success(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut, UInt256 expected, uint fee)
        {
            var market = CreateNewOpdexRouter(StakingMarket, fee);

            var amountIn = market.GetAmountIn(amountOut, reserveIn, reserveOut);

            amountIn.Should().Be(expected);
        }

        [Theory]
        [InlineData(0, 10_000, 100_000)]
        public void GetAmountIn_Throws_InsufficientInputAmount(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut)
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

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
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.GetAmountIn(amountOut, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }

        [Theory]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 33334, 0)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 33407, 1)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 33479, 2)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 33552, 3)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 33625, 4)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 33698, 5)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 33772, 6)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 33845, 7)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 33920, 8)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 33994, 9)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 34069, 10)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7532, 0)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7547, 1)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7561, 2)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7577, 3)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7592, 4)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7608, 5)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7624, 6)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7637, 7)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7653, 8)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7669, 9)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7685, 10)]
        // amountCrs = (tokenOutReserveCrs * tokenOutAmount * 1000) / ((tokenOutReserveSrc - tokenOutAmount) * (1000 - Fee)) + 1
        // expected = (tokenInReserveSrc * amountCrs * 1000) / ((tokenInReserveCrs - amountCrs) * (1000 - Fee)) + 1
        public void GetAmountIn_SrcToSrc_Success(UInt256 tokenOutAmount, UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc,
            UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc, UInt256 expectedTokenInAmountIn, uint fee)
        {
            var market = CreateNewOpdexRouter(StakingMarket, fee);

            var amountIn = market.GetAmountIn(tokenOutAmount, tokenOutReserveCrs, tokenOutReserveSrc, tokenInReserveCrs, tokenInReserveSrc);

            amountIn.Should().Be(expectedTokenInAmountIn);
        }

        [Theory]
        [InlineData(1_000, 0, 100_000, 100_000, 100_000)]
        [InlineData(1_000, 100_000, 0, 100_000, 100_000)]
        [InlineData(1_000, 100_000, 100_000, 0, 100_000)]
        [InlineData(1_000, 100_000, 100_000, 100_000, 0)]
        public void GetAmountIn_SrcToSrc_Throws_InsufficientLiquidity(UInt256 tokenOutAmount, UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc,
            UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc)
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.GetAmountIn(tokenOutAmount, tokenOutReserveCrs, tokenOutReserveSrc, tokenInReserveCrs, tokenInReserveSrc))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }

        [Theory]
        [InlineData(0, 100_000, 100_000, 100_000, 100_000)]
        public void GetAmountIn_SrcToSrc_Throws_InsufficientInputAmount(UInt256 tokenOutAmount, UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc,
            UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc)
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.GetAmountIn(tokenOutAmount, tokenOutReserveCrs, tokenOutReserveSrc, tokenInReserveCrs, tokenInReserveSrc))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        }

        [Theory]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 20_000, 0)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 19_965, 1)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 19_931, 2)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 19_897, 3)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 19_864, 4)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 19_829, 5)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 19_796, 6)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 19_762, 7)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 19_728, 8)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 19_694, 9)]
        [InlineData(25_000, 450_000, 200_000, 450_000, 200_000, 19_660, 10)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_468, 0)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_453, 1)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_439, 2)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_424, 3)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_409, 4)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_393, 5)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_380, 6)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_365, 7)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_349, 8)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_336, 9)]
        [InlineData(5_000, 1_500_000, 2_000_000, 2_500_000, 5_000_000, 7_321, 10)]
        // amountCrs = (tokenInAmount * (1000 - Fee) * tokenInReserveCrs) / (tokenInReserveSrc * 1000 + (tokenInAmount * (1000 - Fee)))
        // expected = (amountCrs * (1000 - Fee) * tokenOutReserveSrc) / (tokenOutReserveSrc * 1000 + (amountCrs * (1000 - Fee)))
        public void GetAmountOut_SrcToSrc_Success(UInt256 tokenInAmount, UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc,
            UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc, UInt256 expectedTokenOutAmount, uint fee)
        {
            var market = CreateNewOpdexRouter(StakingMarket, fee);

            var amountIn = market.GetAmountOut(tokenInAmount, tokenInReserveCrs, tokenInReserveSrc, tokenOutReserveCrs, tokenOutReserveSrc);

            amountIn.Should().Be(expectedTokenOutAmount);
        }

        [Theory]
        [InlineData(1_000, 0, 100_000, 100_000, 100_000)]
        [InlineData(1_000, 100_000, 0, 100_000, 100_000)]
        [InlineData(1_000, 100_000, 100_000, 0, 100_000)]
        [InlineData(1_000, 100_000, 100_000, 100_000, 0)]
        public void GetAmountOut_SrcToSrc_Throws_InsufficientLiquidity(UInt256 tokenInAmount, UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc,
            UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc)
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.GetAmountOut(tokenInAmount, tokenOutReserveCrs, tokenOutReserveSrc, tokenInReserveCrs, tokenInReserveSrc))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }

        [Theory]
        [InlineData(0, 100_000, 100_000, 100_000, 100_000)]
        public void GetAmountOut_SrcToSrc_Throws_InsufficientInputAmount(UInt256 tokenInAmount, UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc,
            UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc)
        {
            var market = CreateNewOpdexRouter(StakingMarket, 3);

            market
                .Invoking(c => c.GetAmountOut(tokenInAmount, tokenOutReserveCrs, tokenOutReserveSrc, tokenInReserveCrs, tokenInReserveSrc))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        }

        #endregion
    }
}
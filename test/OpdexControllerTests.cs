using System;
using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Xunit;

namespace OpdexCoreContracts.Tests
{
    public class OpdexControllerTests : BaseContractTest
    {
        [Fact]
        public void CreatesNewController_Success()
        {
            CreateNewOpdexController();
        }

        #region Pool

        [Fact]
        public void GetPool_Success()
        {
            var controller = CreateNewOpdexController();
            PersistentState.SetContract(Pool, true);
            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            controller.GetPool(Token).Should().Be(Pool);
        }

        [Fact]
        public void CreatesPoolWithStakeToken_Success()
        {
            var controller = CreateNewOpdexController();
            PersistentState.SetContract(Token, true);
            PersistentState.SetAddress(nameof(StakeToken), StakeToken);

            SetupCreate<OpdexStakingPool>(CreateResult.Succeeded(Pool), parameters: new object[] {Token, StakeToken});

            var pool = controller.CreatePool(Token);

            controller.GetPool(Token)
                .Should().Be(pool)
                .And.Be(Pool);

            var expectedPoolCreatedEvent = new OpdexPoolCreatedEvent { Token = Token, Pool = Pool };
            VerifyLog(expectedPoolCreatedEvent, Times.Once);
        }
        
        [Fact]
        public void CreatesPoolWithoutStakeToken_Success()
        {
            var controller = CreateNewOpdexController();
            PersistentState.SetContract(Token, true);

            SetupCreate<OpdexStakingPool>(CreateResult.Succeeded(Pool), parameters: new object[] {Token, StakeToken});

            var pool = controller.CreatePool(Token);

            controller.GetPool(Token)
                .Should().Be(pool)
                .And.Be(Pool);

            var expectedPoolCreatedEvent = new OpdexPoolCreatedEvent { Token = Token, Pool = Pool };
            VerifyLog(expectedPoolCreatedEvent, Times.Once);
        }

        [Fact]
        public void CreatesPool_Throws_ZeroAddress()
        {
            var token = Address.Zero;
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.CreatePool(token))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: ZERO_ADDRESS");
        }
        
        [Fact]
        public void CreatesPool_Throws_PoolExists()
        {
            var controller = CreateNewOpdexController();
            PersistentState.SetContract(Token, true);
            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            controller
                .Invoking(c => c.CreatePool(Token))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: POOL_EXISTS");
        }
        
        #endregion

        #region Add Liquidity

        [Theory]
        [InlineData(1_000, 10_000, 990, 9_900)]
        [InlineData(50000000, 950000000, 45000000, 85000000)]
        public void AddLiquidity_Success_NoReserves(ulong amountCrsDesired, UInt256 amountTokenDesired, ulong amountCrsMin, UInt256 amountTokenMin)
        {
            var to = OtherAddress;
            
            // Tests specific flows where there are no existing reserves
            var expectedReserveCrs = UInt256.MinValue;
            var expectedReserveToken = UInt256.MinValue;
            
            var controller = CreateNewOpdexController();

            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress, amountCrsDesired);

            // Call to get reserves from pool
            var expectedReserves = new [] { Serializer.Serialize(expectedReserveCrs), Serializer.Serialize(expectedReserveToken) };
            SetupCall(Pool, 0, "get_Reserves", null, TransferResult.Transferred(expectedReserves));
            
            // Transfer SRC to Pool
            var transferFromParams = new object[] {OtherAddress, Pool, amountTokenDesired};
            SetupCall(Token, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));

            // Mint Liquidity Tokens
            var mintParams = new object[] {to};
            // It is not this tests responsibility to validate the minted liquidity tokens amounts
            SetupCall(Pool, amountCrsDesired, "Mint", mintParams, TransferResult.Transferred(It.IsAny<UInt256>()));

            var addLiquidityResponse = controller.AddLiquidity(Token, amountTokenDesired, amountCrsMin, amountTokenMin, to, 0ul);

            addLiquidityResponse[0].Should().Be(amountCrsDesired);
            addLiquidityResponse[1].Should().Be(amountTokenDesired);
            // It is not this tests responsibility to validate the returned minted liquidity tokens
            addLiquidityResponse[2].Should().Be(It.IsAny<UInt256>());
            
            VerifyCall(Pool, 0, "get_Reserves", null, Times.Once);
            VerifyCall(Token, 0, "TransferFrom", transferFromParams, Times.Once);
            VerifyCall(Pool, amountCrsDesired, "Mint", mintParams, Times.Once);
        }
        
        [Theory]
        [InlineData(1_000, 1_500, 500, 750, 100_000, 150_000)]
        [InlineData(25_000, 75_000, 20_000, 60_000, 2_500_000, 7_500_000)]
        public void AddLiquidity_Success_ExistingReserves_TokenOptimal(ulong amountCrsDesired, UInt256 amountTokenDesired, ulong amountCrsMin, 
            UInt256 amountTokenMin, UInt256 reserveCrs, UInt256 reserveToken)
        {
            var to = OtherAddress;
            
            var controller = CreateNewOpdexController();

            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress, amountCrsDesired);

            // Call to get reserves from pool
            var expectedReserves = new [] { Serializer.Serialize(reserveCrs), Serializer.Serialize(reserveToken) };
            SetupCall(Pool, 0, "get_Reserves", null, TransferResult.Transferred(expectedReserves));
            
            // Transfer SRC to Pool
            var expectedAmountSrcOptimal = controller.GetLiquidityQuote(amountCrsDesired, reserveCrs, reserveToken);
            var transferFromParams = new object[] {OtherAddress, Pool, expectedAmountSrcOptimal};
            SetupCall(Token, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));

            // Mint Liquidity Tokens
            var mintParams = new object[] {to};
            // It is not this tests responsibility to validate the minted liquidity tokens amounts
            SetupCall(Pool, amountCrsDesired, "Mint", mintParams, TransferResult.Transferred(It.IsAny<UInt256>()));
            
            var addLiquidityResponse = controller.AddLiquidity(Token, amountTokenDesired, amountCrsMin, amountTokenMin, to, 0ul);

            addLiquidityResponse[0].Should().Be(amountCrsDesired);
            addLiquidityResponse[1].Should().Be(expectedAmountSrcOptimal);
            // It is not this tests responsibility to validate the returned minted liquidity tokens
            addLiquidityResponse[2].Should().Be(It.IsAny<UInt256>());
            
            VerifyCall(Pool, 0, "get_Reserves", null, Times.Once);
            VerifyCall(Token, 0, "TransferFrom", transferFromParams, Times.Once);
            VerifyCall(Pool, amountCrsDesired, "Mint", mintParams, Times.Once);
        }
        
        [Theory]
        [InlineData(1_500, 900, 750, 500, 150_000, 100_000)]
        [InlineData(75_000, 24_000, 60_000, 20_000, 7_500_000, 2_500_000)]
        public void AddLiquidity_Success_ExistingReserves_CrsOptimal(ulong amountCrsDesired, UInt256 amountTokenDesired, ulong amountCrsMin, 
            UInt256 amountTokenMin, UInt256 reserveCrs, UInt256 reserveToken)
        {
            var to = OtherAddress;
            
            var controller = CreateNewOpdexController();

            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress, amountCrsDesired);

            // Call to get reserves from pool
            var expectedReserves = new [] { Serializer.Serialize(reserveCrs), Serializer.Serialize(reserveToken) };
            SetupCall(Pool, 0, "get_Reserves", null, TransferResult.Transferred(expectedReserves));
            
            // Transfer SRC to Pool
            // CrsOptimal route always uses amountTokenDesired
            var transferFromParams = new object[] {OtherAddress, Pool, amountTokenDesired};
            SetupCall(Token, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            var expectedAmountCrsOptimal = (ulong)controller.GetLiquidityQuote(amountTokenDesired, reserveToken, reserveCrs);
            
            // Mint Liquidity Tokens
            var mintParams = new object[] {to};
            // It is not this tests responsibility to validate the minted liquidity tokens amounts
            SetupCall(Pool, expectedAmountCrsOptimal, "Mint", mintParams, TransferResult.Transferred(It.IsAny<UInt256>()));
            
            // Transfer CRS change back to sender
            var change = amountCrsDesired - expectedAmountCrsOptimal;
            SetupTransfer(OtherAddress, change, TransferResult.Transferred(true));

            var addLiquidityResponse = controller.AddLiquidity(Token, amountTokenDesired, amountCrsMin, amountTokenMin, to, 0ul);

            addLiquidityResponse[0].Should().Be(expectedAmountCrsOptimal);
            addLiquidityResponse[1].Should().Be(amountTokenDesired);
            // It is not this tests responsibility to validate the returned minted liquidity tokens
            addLiquidityResponse[2].Should().Be(It.IsAny<UInt256>());
            
            VerifyCall(Pool, 0, "get_Reserves", null, Times.Once);
            VerifyCall(Token, 0, "TransferFrom", transferFromParams, Times.Once);
            VerifyCall(Pool, expectedAmountCrsOptimal, "Mint", mintParams, Times.Once);
            VerifyTransfer(OtherAddress, amountCrsDesired - expectedAmountCrsOptimal, Times.Once);
        }

        [Fact]
        public void AddLiquidity_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.AddLiquidity(Token, 10, 10, 10, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }
        
        #endregion

        #region Remove Liquidity

        [Theory]
        [InlineData(100, 1_000, 1_000)]
        public void RemoveLiquidity_Success(UInt256 liquidity, ulong amountCrsMin, UInt256 amountTokenMin)
        {
            var controller = CreateNewOpdexController();

            PersistentState.SetAddress($"Pool:{Token}", Pool);

            SetupMessage(Controller, OtherAddress);
            
            // Transfer Liquidity tokens to pool
            var transferFromParams = new object[] {OtherAddress, Pool, liquidity};
            SetupCall(Pool, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Burn liquidity tokens
            var burnParams = new object[] {OtherAddress};
            var expectedBurnResponse = new [] { amountCrsMin, amountTokenMin };
            SetupCall(Pool, 0, "Burn", burnParams, TransferResult.Transferred(expectedBurnResponse));

            var removeLiquidityResponse = controller.RemoveLiquidity(Token, liquidity, amountCrsMin, amountCrsMin, OtherAddress, 0ul);

            removeLiquidityResponse[0].Should().Be(amountCrsMin);
            removeLiquidityResponse[1].Should().Be(amountTokenMin);
            
            VerifyCall(Pool, 0, "TransferFrom", transferFromParams, Times.Once);
            VerifyCall(Pool, 0, "Burn", burnParams, Times.Once);
        }

        [Fact]
        public void RemoveLiquidity_Throws_InvalidPool()
        {
            var controller = CreateNewOpdexController();
            
            SetupMessage(Controller, OtherAddress);
            
            controller
                .Invoking(c => c.RemoveLiquidity(Token, 100, 1000, 1000, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }
        
        [Fact]
        public void RemoveLiquidity_Throws_InsufficientCrsAmount()
        {
            UInt256 liquidity = 100;
            const ulong amountCrsMin = 1000;
            UInt256 amountTokenMin = 1000;
            var controller = CreateNewOpdexController();

            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Transfer Liquidity tokens to pool
            var transferFromParams = new object[] {OtherAddress, Pool, liquidity};
            SetupCall(Pool, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Burn liquidity tokens
            var burnParams = new object[] {OtherAddress};
            var expectedAmountCrsMin = amountCrsMin - 1;
            var expectedBurnResponse = new [] { expectedAmountCrsMin, amountTokenMin };
            SetupCall(Pool, 0, "Burn", burnParams, TransferResult.Transferred(expectedBurnResponse));
            
            controller
                .Invoking(c => c.RemoveLiquidity(Token, liquidity, amountCrsMin, amountTokenMin, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_CRS_AMOUNT");
        }
        
        [Fact]
        public void RemoveLiquidity_Throws_InsufficientTokenAmount()
        {
            UInt256 liquidity = 100;
            const ulong amountCrsMin = 1000;
            UInt256 amountTokenMin = 1000;
            var controller = CreateNewOpdexController();

            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Transfer Liquidity tokens to pool
            var transferFromParams = new object[] {OtherAddress, Pool, liquidity};
            SetupCall(Pool, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Burn liquidity tokens
            var burnParams = new object[] {OtherAddress};
            var expectedAmountSrcMin = amountTokenMin - 1;
            var expectedBurnResponse = new [] { amountCrsMin, expectedAmountSrcMin };
            SetupCall(Pool, 0, "Burn", burnParams, TransferResult.Transferred(expectedBurnResponse));
            
            controller
                .Invoking(c => c.RemoveLiquidity(Token, liquidity, amountCrsMin, amountTokenMin, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_SRC_AMOUNT");
        }
        
        [Fact]
        public void RemoveLiquidity_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.RemoveLiquidity(Token, UInt256.MaxValue, ulong.MaxValue, UInt256.MaxValue, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }
        
        #endregion

        #region Swap Exact CRS for Tokens

        [Theory]
        [InlineData(6500, 17_000, 200_000, 450_000)]
        public void SwapExactCrsForSrc_Success(UInt256 amountTokenOutMin, ulong amountCrsIn, UInt256 reserveToken, ulong reserveCrs)
        {
            // Arrange
            var controller = CreateNewOpdexController();
            
            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress, amountCrsIn);
            
            // Call to get reserves from pool
            var expectedReserves = new [] { Serializer.Serialize(reserveCrs), Serializer.Serialize(reserveToken) };
            SetupCall(Pool, 0, "get_Reserves", null, TransferResult.Transferred(expectedReserves));
            
            // Calculate actual amount out based on the provided input amount of crs - separate tests for accuracy for this method specifically
            var amountOut = controller.GetAmountOut(amountCrsIn, reserveCrs, reserveToken);

            // Call pool to swap
            var swapParams = new object[] {0ul, amountOut, OtherAddress, new byte[0]};
            SetupCall(Pool, amountCrsIn, "Swap", swapParams, TransferResult.Transferred(true));
            
            // Act
            controller.SwapExactCrsForSrc(amountTokenOutMin, Token, OtherAddress, 0);
            
            // Assert
            VerifyCall(Pool, 0, "get_Reserves", null, Times.Once);
            VerifyCall(Pool, amountCrsIn, "Swap", swapParams, Times.Once);
        }

        [Fact]
        public void SwapExactCrsForSrc_Throws_InvalidPool()
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.SwapExactCrsForSrc(1000, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }
        
        [Theory]
        [InlineData(6500, 14625, 200_000, 450_000)]
        public void SwapExactCrsForTokens_Throws_InsufficientOutputAmount(UInt256 amountTokenOutMin, ulong amountCrsIn, UInt256 reserveToken, UInt256 reserveCrs)
        {
            var controller = CreateNewOpdexController();
            
            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress, amountCrsIn);
            
            // Call to get reserves from pool
            var expectedReserves = new [] { Serializer.Serialize(reserveCrs), Serializer.Serialize(reserveToken) };
            SetupCall(Pool, 0, "get_Reserves", null, TransferResult.Transferred(expectedReserves));
            
            controller
                .Invoking(c => c.SwapExactCrsForSrc(amountTokenOutMin, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        }
        
        [Fact]
        public void SwapExactCrsForSrc_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.SwapExactCrsForSrc(10ul, Token, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }
        
        #endregion

        #region Swap Tokens for Exact CRS

        [Theory]
        [InlineData(6500, 17_000, 200_000, 450_000)]
        public void SwapSrcForExactCrs_Success(ulong amountCrsOut, UInt256 amountTokenInMax, UInt256 reserveToken, ulong reserveCrs)
        {
            // Arrange
            var controller = CreateNewOpdexController();
            
            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Call to get reserves from pool
            var expectedReserves = new [] { Serializer.Serialize(reserveCrs), Serializer.Serialize(reserveToken) };
            SetupCall(Pool, 0, "get_Reserves", null, TransferResult.Transferred(expectedReserves));

            // Calculate actual amount out based on the provided input amount of crs - separate tests for accuracy for this method specifically
            var amountIn = controller.GetAmountIn(amountCrsOut, reserveToken, reserveCrs);
            
            // Call token to Transfer from caller to Pool
            var transferFromParams = new object[] { OtherAddress, Pool, amountIn };
            SetupCall(Token, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Call pool to swap
            var swapParams = new object[] {amountCrsOut, UInt256.MinValue, OtherAddress, new byte[0]};
            SetupCall(Pool, 0, "Swap", swapParams, TransferResult.Transferred(true));
            
            // Act
            controller.SwapSrcForExactCrs(amountCrsOut, amountIn, Token, OtherAddress, 0);
            
            // Assert
            VerifyCall(Pool, 0, "get_Reserves", null, Times.Once);
            VerifyCall(Token, 0, "TransferFrom", transferFromParams, Times.Once);
            VerifyCall(Pool, 0, "Swap", swapParams, Times.Once);
        }

        [Fact]
        public void SwapSrcForExactCrs_Throws_InvalidPool()
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.SwapSrcForExactCrs(1000, 1000, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }
        
        [Theory]
        [InlineData(6500, 2000, 200_000, 450_000)]
        public void SwapSrcForExactCrs_Throws_ExcessiveInputAmount(ulong amountCrsOut, UInt256 amountTokenInMax, UInt256 reserveToken, ulong reserveCrs)
        {
            var controller = CreateNewOpdexController();
            
            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Call to get reserves from pool
            var expectedReserves = new [] { Serializer.Serialize(reserveCrs), Serializer.Serialize(reserveToken) };
            SetupCall(Pool, 0, "get_Reserves", null, TransferResult.Transferred(expectedReserves));
            
            controller
                .Invoking(c => c.SwapSrcForExactCrs(amountCrsOut, amountTokenInMax, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXCESSIVE_INPUT_AMOUNT");
        }
        
        [Fact]
        public void SwapSrcForExactCrs_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.SwapSrcForExactCrs(10ul, UInt256.MaxValue, Token, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }
        
        #endregion

        #region Swap Exact Tokens for CRS

        [Theory]
        [InlineData(8000, 17_000, 200_000, 450_000)]
        public void SwapExactSrcForCrs_Success(UInt256 amountTokenIn, ulong amountCrsOutMin, UInt256 reserveToken, UInt256 reserveCrs)
        {
            // Arrange
            var controller = CreateNewOpdexController();
            
            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Call to get reserves from pool
            var expectedReserves = new [] { Serializer.Serialize(reserveCrs), Serializer.Serialize(reserveToken) };
            SetupCall(Pool, 0, "get_Reserves", null, TransferResult.Transferred(expectedReserves));

            // Calculate actual amount out based on the provided input amount of crs - separate tests for accuracy for this method specifically
            var amountOut = controller.GetAmountOut(amountTokenIn, reserveToken, reserveCrs);
            
            // Call token to Transfer from caller to Pool
            var transferFromParams = new object[] { OtherAddress, Pool, amountTokenIn };
            SetupCall(Token, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Call pool to swap
            var swapParams = new object[] {(ulong)amountOut, UInt256.MinValue, OtherAddress, new byte[0]};
            SetupCall(Pool, 0, "Swap", swapParams, TransferResult.Transferred(true));
            
            // Act
            controller.SwapExactSrcForCrs(amountTokenIn, amountCrsOutMin, Token, OtherAddress, 0);
            
            // Assert
            VerifyCall(Pool, 0, "get_Reserves", null, Times.Once);
            VerifyCall(Token, 0, "TransferFrom", transferFromParams, Times.Once);
            VerifyCall(Pool, 0, "Swap", swapParams, Times.Once);
        }

        [Fact]
        public void SwapExactSrcForCrs_Throws_InvalidPool()
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.SwapExactSrcForCrs(1000, 1000, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }
        
        [Theory]
        [InlineData(6500, 20000, 200_000, 450_000)]
        public void SwapExactSrcForCrs_Throws_InsufficientOutputAmount(UInt256 amountTokenIn, ulong amountCrsOutMin, UInt256 reserveToken, ulong reserveCrs)
        {
            var controller = CreateNewOpdexController();

            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Call to get reserves from pool
            var expectedReserves = new [] { Serializer.Serialize(reserveCrs), Serializer.Serialize(reserveToken) };
            SetupCall(Pool, 0, "get_Reserves", null, TransferResult.Transferred(expectedReserves));
            
            controller
                .Invoking(c => c.SwapExactSrcForCrs(amountTokenIn, amountCrsOutMin, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        }
        
        [Fact]
        public void SwapExactSrcForCrs_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.SwapExactSrcForCrs(10ul, 10ul, Token, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }
        
        #endregion

        #region Swap CRS for Exact Tokens

        [Theory]
        [InlineData(24_000, 10_000, 200_000, 450_000)]
        public void SwapCrsForExactSrc_Success(ulong amountCrsIn, UInt256 amountTokenOut, UInt256 reserveToken, ulong reserveCrs)
        {
            // Arrange
            var controller = CreateNewOpdexController();
            
            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress, amountCrsIn);
            
            // Call to get reserves from pool
            var expectedReserves = new [] { Serializer.Serialize(reserveCrs), Serializer.Serialize(reserveToken) };
            SetupCall(Pool, 0, "get_Reserves", null, TransferResult.Transferred(expectedReserves));

            var amountIn = (ulong)controller.GetAmountIn(amountTokenOut, reserveCrs, reserveToken);

            var change = amountCrsIn - amountIn;
            
            // Call pool to swap
            var swapParams = new object[] {0ul, amountTokenOut, OtherAddress, new byte[0]};
            SetupCall(Pool, amountIn, "Swap", swapParams, TransferResult.Transferred(true));

            if (change > 0)
            {
                SetupTransfer(OtherAddress, change, TransferResult.Transferred(true));
            }
            
            // Act
            controller.SwapCrsForExactSrc(amountTokenOut, Token, OtherAddress, 0);
            
            // Assert
            VerifyCall(Pool, 0, "get_Reserves", null, Times.Once);
            VerifyCall(Pool, amountIn, "Swap", swapParams, Times.Once);

            if (change > 0)
            {
                VerifyTransfer(OtherAddress, change, Times.Once);
            }
        }

        [Fact]
        public void SwapCrsForExactSrc_Throws_InvalidPool()
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.SwapCrsForExactSrc(1000, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INVALID_POOL");
        }
        
        [Theory]
        [InlineData(6500, 2000, 200_000, 450_000)]
        public void SwapCrsForExactSrc_Throws_ExcessiveInputAmount(UInt256 amountCrsIn, UInt256 amountTokenOut, UInt256 reserveToken, UInt256 reserveCrs)
        {
            var controller = CreateNewOpdexController();
            
            PersistentState.SetAddress($"Pool:{Token}", Pool);
            
            SetupMessage(Controller, OtherAddress);
            
            var expectedReserves = new [] { Serializer.Serialize(reserveCrs), Serializer.Serialize(reserveToken) };
            SetupCall(Pool, 0, "get_Reserves", null, TransferResult.Transferred(expectedReserves));
            
            controller
                .Invoking(c => c.SwapCrsForExactSrc(amountTokenOut, Token, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXCESSIVE_INPUT_AMOUNT");
        }
        
        [Fact]
        public void SwapCrsForExactSrc_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.SwapCrsForExactSrc(UInt256.MaxValue, Token, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }

        #endregion
        
        #region Swap SRC for Exact SRC Tokens
        
        [Theory]
        [InlineData(24_000, 107_000, 200_000, 450_000, 200_000, 450_000)]
        public void SwapSrcForExactSrc_Success(UInt256 amountSrcOut, UInt256 amountSrcInMax, 
            UInt256 reserveSrcIn, ulong reserveCrsIn, UInt256 reserveSrcOut, ulong reserveCrsOut)
        {
            var tokenIn = Token;
            var tokenOut = TokenTwo;
            var tokenInPool = Pool;
            var tokenOutPool = PoolTwo;
            
            // Arrange
            var controller = CreateNewOpdexController();
            
            PersistentState.SetAddress($"Pool:{tokenIn}", tokenInPool);
            PersistentState.SetAddress($"Pool:{tokenOut}", tokenOutPool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Call to get reserves from pool
            var expectedTokenInReserves = new [] { Serializer.Serialize(reserveCrsIn), Serializer.Serialize(reserveSrcIn) };
            SetupCall(tokenInPool, 0, "get_Reserves", null, TransferResult.Transferred(expectedTokenInReserves));
            
            // Call to get reserves from pool
            var expectedTokenOutReserves = new [] { Serializer.Serialize(reserveCrsOut), Serializer.Serialize(reserveSrcOut) };
            SetupCall(tokenOutPool, 0, "get_Reserves", null, TransferResult.Transferred(expectedTokenOutReserves));
        
            var amountCrsIn = (ulong)controller.GetAmountIn(amountSrcOut, reserveCrsOut, reserveSrcOut);
            var amountSrcIn = controller.GetAmountOut(amountCrsIn, reserveSrcIn, reserveCrsIn);
            
            // Transfer SRC for CRS
            var transferFromParams = new object[] { OtherAddress, tokenInPool, amountSrcIn };
            SetupCall(tokenIn, 0ul, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Call pool to swap src to crs
            var swapSrcToCrsParams = new object[] {amountCrsIn, UInt256.MinValue, Controller, new byte[0]};
            SetupCall(tokenInPool, 0, "Swap", swapSrcToCrsParams, TransferResult.Transferred(true), () => SetupBalance(amountCrsIn));
            
            // Call pool to swap crs to src
            var swapCrsToSrcParams = new object[] { 0ul, amountSrcOut, OtherAddress, new byte[0]};
            SetupCall(tokenOutPool, 0, "Swap", swapCrsToSrcParams, TransferResult.Transferred(true));
            
            // Act
            controller.SwapSrcForExactSrc(amountSrcInMax, tokenIn, amountSrcOut, tokenOut, OtherAddress, 0);
            
            // Assert
            VerifyCall(tokenInPool, 0, "get_Reserves", null, Times.Once);
            VerifyCall(tokenOutPool, 0, "get_Reserves", null, Times.Once);
            VerifyCall(tokenInPool, 0, "Swap", swapSrcToCrsParams, Times.Once);
            VerifyCall(tokenOutPool, 0, "Swap", swapCrsToSrcParams, Times.Once);
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
            var controller = CreateNewOpdexController();
            
            PersistentState.SetAddress($"Pool:{tokenIn}", tokenInPool);
            PersistentState.SetAddress($"Pool:{tokenOut}", tokenOutPool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Call to get reserves from pool
            var expectedTokenInReserves = new [] { Serializer.Serialize(reserveCrsIn), Serializer.Serialize(reserveSrcIn) };
            SetupCall(tokenInPool, 0, "get_Reserves", null, TransferResult.Transferred(expectedTokenInReserves));
            
            // Call to get reserves from pool
            var expectedTokenOutReserves = new [] { Serializer.Serialize(reserveCrsOut), Serializer.Serialize(reserveSrcOut) };
            SetupCall(tokenOutPool, 0, "get_Reserves", null, TransferResult.Transferred(expectedTokenOutReserves));
            
            controller
                .Invoking(c => c.SwapSrcForExactSrc(amountSrcInMax, tokenIn, amountSrcOut, tokenOut, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        }
        
        [Fact]
        public void SwapSrcForExactSrc_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.SwapSrcForExactSrc(UInt256.MaxValue, Token, UInt256.MaxValue, TokenTwo, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
        }
        
        #endregion
        
        #region Swap Exact SRC for SRC Tokens
        
        [Theory]
        [InlineData(24_000, 19_000, 200_000, 450_000, 200_000, 450_000)]
        [InlineData(100_000_000_000, 1_000_000, 2_500_000_000_000, 10_000_000_000, 2_500_000_000_000, 10_000_000_000)]
        public void SwapExactSrcForSrc_Success(UInt256 amountSrcIn, UInt256 amountSrcOutMin, 
            UInt256 reserveSrcIn, ulong reserveCrsIn, UInt256 reserveSrcOut, ulong reserveCrsOut)
        {
            var tokenIn = Token;
            var tokenOut = TokenTwo;
            var tokenInPool = Pool;
            var tokenOutPool = PoolTwo;
            
            // Arrange
            var controller = CreateNewOpdexController();
            
            PersistentState.SetAddress($"Pool:{tokenIn}", tokenInPool);
            PersistentState.SetAddress($"Pool:{tokenOut}", tokenOutPool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Call to get reserves from pool
            var expectedTokenInReserves = new [] { Serializer.Serialize(reserveCrsIn), Serializer.Serialize(reserveSrcIn) };
            SetupCall(tokenInPool, 0, "get_Reserves", null, TransferResult.Transferred(expectedTokenInReserves));
            
            // Call to get reserves from pool
            var expectedTokenOutReserves = new [] { Serializer.Serialize(reserveCrsOut), Serializer.Serialize(reserveSrcOut) };
            SetupCall(tokenOutPool, 0, "get_Reserves", null, TransferResult.Transferred(expectedTokenOutReserves));
        
            var amountCrsOut = (ulong)controller.GetAmountOut(amountSrcIn, reserveSrcOut, reserveCrsOut);
            var amountSrcOut = controller.GetAmountOut(amountCrsOut, reserveCrsIn, reserveSrcIn);
            
            // Transfer SRC for CRS
            var transferFromParams = new object[] { OtherAddress, tokenInPool, amountSrcIn };
            SetupCall(tokenIn, 0ul, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
        
            // Call pool to swap src to crs
            var swapSrcToCrsParams = new object[] {amountCrsOut, UInt256.MinValue, Controller, new byte[0]};
            SetupCall(tokenInPool, 0, "Swap", swapSrcToCrsParams, TransferResult.Transferred(true), () => SetupBalance(amountCrsOut));
            
            // Call pool to swap crs to src
            var swapCrsToSrcParams = new object[] { 0ul, amountSrcOut, OtherAddress, new byte[0]};
            SetupCall(tokenOutPool, 0, "Swap", swapCrsToSrcParams, TransferResult.Transferred(true));
            
            // Act
            controller.SwapExactSrcForSrc(amountSrcIn, tokenIn, amountSrcOutMin, tokenOut, OtherAddress, 0);
            
            // Assert
            VerifyCall(tokenInPool, 0, "get_Reserves", null, Times.Once);
            VerifyCall(tokenOutPool, 0, "get_Reserves", null, Times.Once);
            VerifyCall(tokenInPool, 0, "Swap", swapSrcToCrsParams, Times.Once);
            VerifyCall(tokenOutPool, 0, "Swap", swapCrsToSrcParams, Times.Once);
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
            var controller = CreateNewOpdexController();
            
            PersistentState.SetAddress($"Pool:{tokenIn}", tokenInPool);
            PersistentState.SetAddress($"Pool:{tokenOut}", tokenOutPool);
            
            SetupMessage(Controller, OtherAddress);
            
            // Call to get reserves from pool
            var expectedTokenInReserves = new [] { Serializer.Serialize(reserveCrsIn), Serializer.Serialize(reserveSrcIn) };
            SetupCall(tokenInPool, 0, "get_Reserves", null, TransferResult.Transferred(expectedTokenInReserves));
            
            // Call to get reserves from pool
            var expectedTokenOutReserves = new [] { Serializer.Serialize(reserveCrsOut), Serializer.Serialize(reserveSrcOut) };
            SetupCall(tokenOutPool, 0, "get_Reserves", null, TransferResult.Transferred(expectedTokenOutReserves));
            
            controller
                .Invoking(c => c.SwapExactSrcForSrc(amountSrcIn, tokenIn, amountSrcOutMin, tokenOut, OtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        }
        
        [Fact]
        public void SwapExactSrcForSrc_Throws_ExpiredDeadline()
        {
            const ulong deadline = 1;
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.SwapExactSrcForSrc(UInt256.MaxValue, Token, UInt256.MaxValue, TokenTwo, Trader0, deadline))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: EXPIRED_DEADLINE");
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
            var controller = CreateNewOpdexController();
            var expected = amountA * reserveB / reserveA;
            var quote = controller.GetLiquidityQuote(amountA, reserveA, reserveB);

            quote.Should().Be(expected);
        }

        [Fact]
        public void GetLiquidityQuote_Throws_InsufficientAmount()
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.GetLiquidityQuote(0, 10, 100))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_AMOUNT");
        }
        
        [Theory]
        [InlineData(10, 1000, 0)]
        [InlineData(10, 0, 1000)]
        public void GetLiquidityQuote_Throws_InsufficientLiquidity(UInt256 amountA, UInt256 reserveA, UInt256 reserveB)
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.GetLiquidityQuote(amountA, reserveA, reserveB))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }

        [Theory]
        [InlineData(1_000, 10_000, 100_000, 9_066)]
        // Todo: Add more scenarios with precalculated expected results
        public void GetAmountOut_Success(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut, UInt256 expected)
        {
            var controller = CreateNewOpdexController();

            var amountOut = controller.GetAmountOut(amountIn, reserveIn, reserveOut);

            amountOut.Should().Be(expected);
        }
        
        [Theory]
        [InlineData(0, 10_000, 100_000)]
        public void GetAmountOut_Throws_InsufficientInputAmount(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut)
        {
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.GetAmountOut(amountIn, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        }
        
        [Theory]
        [InlineData(1_000, 0, 100_000)]
        [InlineData(1_000, 10_000, 0)]
        public void GetAmountOut_Throws_InsufficientLiquidity(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut)
        {
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.GetAmountOut(amountIn, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }
        
        [Theory]
        [InlineData(10_000, 10_000, 100_000, 1_115)]
        // Todo: Add more scenarios with precalculated expected results
        public void GetAmountIn_Success(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut, UInt256 expected)
        {
            var controller = CreateNewOpdexController();

            var amountIn = controller.GetAmountIn(amountOut, reserveIn, reserveOut);

            amountIn.Should().Be(expected);
        }
        
        [Theory]
        [InlineData(0, 10_000, 100_000)]
        public void GetAmountIn_Throws_InsufficientInputAmount(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut)
        {
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.GetAmountIn(amountOut, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        }
        
        [Theory]
        [InlineData(1_000, 0, 100_000)]
        [InlineData(1_000, 10_000, 0)]
        public void GetAmountIn_Throws_InsufficientLiquidity(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut)
        {
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.GetAmountIn(amountOut, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OPDEX: INSUFFICIENT_LIQUIDITY");
        }
        
        #endregion
    }
}
using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Xunit;
using Stratis.SmartContracts.CLR;

namespace OpdexV1Contracts.Tests
{
    public class OpdexV1ControllerTests : BaseContractTest
    {
        [Fact]
        public void CreatesNewController_Success()
        {
            var controller = CreateNewOpdexController();
            controller.FeeTo.Should().Be(_feeTo);
            controller.FeeToSetter.Should().Be(_feeToSetter);
        }

        #region FeeTo and FeeToSetter

        [Fact]
        public void SetFeeTo_Success()
        {
            var newFeeTo = _otherAddress;
            var controller = CreateNewOpdexController();

            SetupMessage(_controller, _feeToSetter);

            controller.SetFeeTo(newFeeTo);

            controller.FeeTo.Should().Be(newFeeTo);
        }

        [Fact]
        public void SetFeeTo_Throws()
        {
            var newFeeTo = _otherAddress;
            var caller = Address.Zero; // or any other address != _feeToSetter 
            var controller = CreateNewOpdexController();

            SetupMessage(_controller, caller);

            controller
                .Invoking(c => c.SetFeeTo(newFeeTo))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: FORBIDDEN");
        }

        [Fact]
        public void GetFeeTo()
        {
            var controller = CreateNewOpdexController();

            SetupMessage(_controller, _feeToSetter);
            _persistentState.SetAddress("FeeTo", _otherAddress);
            controller.GetFeeTo().Should().Be(_otherAddress);
        }

        [Fact]
        public void SetFeeToSetter_Success()
        {
            var newFeeToSetter = _otherAddress;
            var controller = CreateNewOpdexController();

            SetupMessage(_controller, _feeToSetter);

            controller.SetFeeToSetter(newFeeToSetter);
            controller.FeeToSetter.Should().Be(newFeeToSetter);
        }

        [Fact]
        public void SetFeeToSetter_Throws()
        {
            var caller = Address.Zero; // or any other address != _feeToSetter 
            var newFeeToSetter = _otherAddress;
            var controller = CreateNewOpdexController();

            SetupMessage(_controller, caller);

            controller
                .Invoking(c => c.SetFeeToSetter(newFeeToSetter))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: FORBIDDEN");
        }

        #endregion

        #region Pair

        [Fact]
        public void GetPair_Success()
        {
            var controller = CreateNewOpdexController();
            _persistentState.IsContractResult = true;
            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            controller.GetPair(_token).Should().Be(_pair);
        }

        [Fact]
        public void CreatesPair_Success()
        {
            var controller = CreateNewOpdexController();
            _persistentState.IsContractResult = true;

            SetupCreate<OpdexV1Pair>(CreateResult.Succeeded(_pair), parameters: new object[] {_token});

            var pair = controller.CreatePair(_token);

            controller.GetPair(_token)
                .Should().Be(pair)
                .And.Be(_pair);

            var expectedPairCreatedEvent = new OpdexV1Controller.PairCreatedEvent { Token = _token, Pair = _pair };
            VerifyLog(expectedPairCreatedEvent, Times.Once);
        }

        [Fact]
        public void CreatesPair_Throws_ZeroAddress()
        {
            var token = Address.Zero;
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.CreatePair(token))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: ZERO_ADDRESS");
        }
        
        [Fact]
        public void CreatesPair_Throws_PairExists()
        {
            var controller = CreateNewOpdexController();
            _persistentState.IsContractResult = true;
            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            controller
                .Invoking(c => c.CreatePair(_token))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: PAIR_EXISTS");
        }
        
        #endregion

        #region Add Liquidity

        [Theory]
        [InlineData(1_000, 10_000, 990, 9_900)]
        public void AddLiquidity_Success_NoReserves(ulong amountCrsDesired, ulong amountTokenDesired, ulong amountCrsMin, ulong amountTokenMin)
        {
            var to = _otherAddress;
            
            // Tests specific flows where there are no existing reserves
            const ulong expectedReserveCrs = 0;
            const ulong expectedReserveToken = 0;
            
            var controller = CreateNewOpdexController();

            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            SetupMessage(_controller, _otherAddress, amountCrsDesired);

            // Call to get reserves from pair
            var expectedReserves = new [] { expectedReserveCrs, expectedReserveToken };
            SetupCall(_pair, 0, "GetReserves", null, TransferResult.Transferred(expectedReserves));
            
            // Transfer SRC to Pair
            var transferFromParams = new object[] {_otherAddress, _pair, amountTokenDesired};
            SetupCall(_token, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Transfer CRS to Pair
            SetupTransfer(_pair, amountCrsDesired, TransferResult.Transferred(true));
            
            // Mint Liquidity Tokens
            var mintParams = new object[] {to};
            // It is not this tests responsibility to validate the minted liquidity tokens amounts
            SetupCall(_pair, 0, "Mint", mintParams, TransferResult.Transferred(It.IsAny<ulong>()));

            var addLiquidityResponse = controller.AddLiquidity(_token, amountTokenDesired, amountCrsMin, amountTokenMin, to, 0ul);

            addLiquidityResponse.AmountCrs.Should().Be(amountCrsDesired);
            addLiquidityResponse.AmountToken.Should().Be(amountTokenDesired);
            // It is not this tests responsibility to validate the returned minted liquidity tokens
            addLiquidityResponse.Liquidity.Should().Be(It.IsAny<ulong>());
            
            VerifyCall(_pair, 0, "GetReserves", null, Times.Once);
            VerifyCall(_token, 0, "TransferFrom", transferFromParams, Times.Once);
            VerifyCall(_pair, 0, "Mint", mintParams, Times.Once);
            VerifyTransfer(_pair, amountCrsDesired, Times.Once);
        }
        
        [Theory]
        [InlineData(1_000, 1_500, 500, 750, 100_000, 150_000)]
        [InlineData(25_000, 75_000, 20_000, 60_000, 2_500_000, 7_500_000)]
        public void AddLiquidity_Success_ExistingReserves_TokenOptimal(ulong amountCrsDesired, ulong amountTokenDesired, ulong amountCrsMin, 
            ulong amountTokenMin, ulong reserveCrs, ulong reserveToken)
        {
            var to = _otherAddress;
            
            var controller = CreateNewOpdexController();

            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            SetupMessage(_controller, _otherAddress, amountCrsDesired);

            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            SetupCall(_pair, 0, "GetReserves", null, TransferResult.Transferred(expectedReserves));
            
            // Transfer SRC to Pair
            var expectedAmountTokenOptimal = controller.GetLiquidityQuote(amountCrsDesired, reserveCrs, reserveToken);
            var transferFromParams = new object[] {_otherAddress, _pair, expectedAmountTokenOptimal};
            SetupCall(_token, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Transfer CRS to Pair
            // TokenOptimal route always uses amountCrsDesired
            SetupTransfer(_pair, amountCrsDesired, TransferResult.Transferred(true));
            
            // Mint Liquidity Tokens
            var mintParams = new object[] {to};
            // It is not this tests responsibility to validate the minted liquidity tokens amounts
            SetupCall(_pair, 0, "Mint", mintParams, TransferResult.Transferred(It.IsAny<ulong>()));
            
            var addLiquidityResponse = controller.AddLiquidity(_token, amountTokenDesired, amountCrsMin, amountTokenMin, to, 0ul);

            addLiquidityResponse.AmountCrs.Should().Be(amountCrsDesired);
            addLiquidityResponse.AmountToken.Should().Be(expectedAmountTokenOptimal);
            // It is not this tests responsibility to validate the returned minted liquidity tokens
            addLiquidityResponse.Liquidity.Should().Be(It.IsAny<ulong>());
            
            VerifyCall(_pair, 0, "GetReserves", null, Times.Once);
            VerifyCall(_token, 0, "TransferFrom", transferFromParams, Times.Once);
            VerifyCall(_pair, 0, "Mint", mintParams, Times.Once);
            VerifyTransfer(_pair, amountCrsDesired, Times.Once);
        }
        
        [Theory]
        [InlineData(1_500, 900, 750, 500, 150_000, 100_000)]
        [InlineData(75_000, 24_000, 60_000, 20_000, 7_500_000, 2_500_000)]
        public void AddLiquidity_Success_ExistingReserves_CrsOptimal(ulong amountCrsDesired, ulong amountTokenDesired, ulong amountCrsMin, 
            ulong amountTokenMin, ulong reserveCrs, ulong reserveToken)
        {
            var to = _otherAddress;
            
            var controller = CreateNewOpdexController();

            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            SetupMessage(_controller, _otherAddress, amountCrsDesired);

            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            SetupCall(_pair, 0, "GetReserves", null, TransferResult.Transferred(expectedReserves));
            
            // Transfer SRC to Pair
            // CrsOptimal route always uses amountTokenDesired
            var transferFromParams = new object[] {_otherAddress, _pair, amountTokenDesired};
            SetupCall(_token, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Transfer CRS to Pair
            var expectedAmountCrsOptimal = controller.GetLiquidityQuote(amountTokenDesired, reserveToken, reserveCrs);
            SetupTransfer(_pair, expectedAmountCrsOptimal, TransferResult.Transferred(true));
            
            // Mint Liquidity Tokens
            var mintParams = new object[] {to};
            // It is not this tests responsibility to validate the minted liquidity tokens amounts
            SetupCall(_pair, 0, "Mint", mintParams, TransferResult.Transferred(It.IsAny<ulong>()));
            
            // Transfer CRS change back to sender
            var change = amountCrsDesired - expectedAmountCrsOptimal;
            SetupTransfer(_otherAddress, change, TransferResult.Transferred(true));

            var addLiquidityResponse = controller.AddLiquidity(_token, amountTokenDesired, amountCrsMin, amountTokenMin, to, 0ul);

            addLiquidityResponse.AmountCrs.Should().Be(expectedAmountCrsOptimal);
            addLiquidityResponse.AmountToken.Should().Be(amountTokenDesired);
            // It is not this tests responsibility to validate the returned minted liquidity tokens
            addLiquidityResponse.Liquidity.Should().Be(It.IsAny<ulong>());
            
            VerifyCall(_pair, 0, "GetReserves", null, Times.Once);
            VerifyCall(_token, 0, "TransferFrom", transferFromParams, Times.Once);
            VerifyCall(_pair, 0, "Mint", mintParams, Times.Once);
            VerifyTransfer(_pair, expectedAmountCrsOptimal, Times.Once);
            VerifyTransfer(_otherAddress, amountCrsDesired - expectedAmountCrsOptimal, Times.Once);
        }
        
        #endregion

        #region Remove Liquidity

        [Theory]
        [InlineData(100, 1_000, 1_000)]
        public void RemoveLiquidity_Success(ulong liquidity, ulong amountCrsMin, ulong amountTokenMin)
        {
            var controller = CreateNewOpdexController();

            _persistentState.SetAddress($"Pair:{_token}", _pair);

            SetupMessage(_controller, _otherAddress);
            
            // Transfer Liquidity tokens to pair
            var transferFromParams = new object[] {_otherAddress, _pair, liquidity};
            SetupCall(_pair, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Burn liquidity tokens
            var burnParams = new object[] {_otherAddress};
            var expectedBurnResponse = new [] { amountCrsMin, amountTokenMin };
            SetupCall(_pair, 0, "Burn", burnParams, TransferResult.Transferred(expectedBurnResponse));

            var removeLiquidityResponse = controller.RemoveLiquidity(_token, liquidity, amountCrsMin, amountCrsMin, _otherAddress, 0ul);

            removeLiquidityResponse.AmountCrs.Should().Be(amountCrsMin);
            removeLiquidityResponse.AmountToken.Should().Be(amountTokenMin);
            
            VerifyCall(_pair, 0, "TransferFrom", transferFromParams, Times.Once);
            VerifyCall(_pair, 0, "Burn", burnParams, Times.Once);
        }

        [Fact]
        public void RemoveLiquidity_Throws_InvalidPair()
        {
            var controller = CreateNewOpdexController();
            
            SetupMessage(_controller, _otherAddress);
            
            controller
                .Invoking(c => c.RemoveLiquidity(_token, 100, 1000, 1000, _otherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INVALID_PAIR");
        }
        
        [Fact]
        public void RemoveLiquidity_Throws_InsufficientCrsAmount()
        {
            const ulong liquidity = 100;
            const ulong amountCrsMin = 1000;
            const ulong amountTokenMin = 1000;
            var controller = CreateNewOpdexController();

            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            SetupMessage(_controller, _otherAddress);
            
            // Transfer Liquidity tokens to pair
            var transferFromParams = new object[] {_otherAddress, _pair, liquidity};
            SetupCall(_pair, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Burn liquidity tokens
            var burnParams = new object[] {_otherAddress};
            const ulong expectedAmountCrsMin = amountCrsMin - 1;
            var expectedBurnResponse = new [] { expectedAmountCrsMin, amountTokenMin };
            SetupCall(_pair, 0, "Burn", burnParams, TransferResult.Transferred(expectedBurnResponse));
            
            controller
                .Invoking(c => c.RemoveLiquidity(_token, liquidity, amountCrsMin, amountTokenMin, _otherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INSUFFICIENT_CRS_AMOUNT");
        }
        
        [Fact]
        public void RemoveLiquidity_Throws_InsufficientTokenAmount()
        {
            const ulong liquidity = 100;
            const ulong amountCrsMin = 1000;
            const ulong amountTokenMin = 1000;
            var controller = CreateNewOpdexController();

            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            SetupMessage(_controller, _otherAddress);
            
            // Transfer Liquidity tokens to pair
            var transferFromParams = new object[] {_otherAddress, _pair, liquidity};
            SetupCall(_pair, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Burn liquidity tokens
            var burnParams = new object[] {_otherAddress};
            const ulong expectedAmountTokenMin = amountTokenMin - 1;
            var expectedBurnResponse = new [] { amountCrsMin, expectedAmountTokenMin };
            SetupCall(_pair, 0, "Burn", burnParams, TransferResult.Transferred(expectedBurnResponse));
            
            controller
                .Invoking(c => c.RemoveLiquidity(_token, liquidity, amountCrsMin, amountTokenMin, _otherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INSUFFICIENT_TOKEN_AMOUNT");
        }
        
        #endregion

        #region Swap Exact CRS for Tokens

        [Theory]
        [InlineData(6500, 17_000, 200_000, 450_000)]
        public void SwapExactCrsForTokens_Success(ulong amountTokenOutMin, ulong amountCrsIn, ulong reserveToken, ulong reserveCrs)
        {
            // Arrange
            var controller = CreateNewOpdexController();
            
            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            SetupMessage(_controller, _otherAddress, amountCrsIn);
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            SetupCall(_pair, 0, "GetReserves", null, TransferResult.Transferred(expectedReserves));
            
            // Calculate actual amount out based on the provided input amount of crs - separate tests for accuracy for this method specifically
            var amountOut = controller.GetAmountOut(amountCrsIn, expectedReserves[0], expectedReserves[1]);

            // Transfer CRS to Pair
            SetupTransfer(_pair, amountCrsIn, TransferResult.Transferred(true));
            
            // Call pair to swap
            var swapParams = new object[] {0ul, amountOut, _otherAddress};
            SetupCall(_pair, 0, "Swap", swapParams, TransferResult.Transferred(true));
            
            // Act
            controller.SwapExactCRSForTokens(amountTokenOutMin, _token, _otherAddress, 0);
            
            // Assert
            VerifyCall(_pair, 0, "GetReserves", null, Times.Once);
            VerifyTransfer(_pair, amountCrsIn, Times.Once);
            VerifyCall(_pair, 0, "Swap", swapParams, Times.Once);
        }

        [Fact]
        public void SwapExactCrsForTokens_Throws_InvalidPair()
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.SwapExactCRSForTokens(1000, _token, _otherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INVALID_PAIR");
        }
        
        [Theory]
        [InlineData(6500, 14625, 200_000, 450_000)]
        public void SwapExactCrsForTokens_Throws_InsufficientOutputAmount(ulong amountTokenOutMin, ulong amountCrsIn, ulong reserveToken, ulong reserveCrs)
        {
            var controller = CreateNewOpdexController();
            
            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            SetupMessage(_controller, _otherAddress, amountCrsIn);
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            SetupCall(_pair, 0, "GetReserves", null, TransferResult.Transferred(expectedReserves));
            
            controller
                .Invoking(c => c.SwapExactCRSForTokens(amountTokenOutMin, _token, _otherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");
        }
        
        #endregion

        #region Swap Tokens for Exact CRS

        [Theory]
        [InlineData(6500, 17_000, 200_000, 450_000)]
        public void SwapTokensForExactCRS_Success(ulong amountCrsOut, ulong amountTokenInMax, ulong reserveToken, ulong reserveCrs)
        {
            // Arrange
            var controller = CreateNewOpdexController();
            
            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            SetupMessage(_controller, _otherAddress);
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            SetupCall(_pair, 0, "GetReserves", null, TransferResult.Transferred(expectedReserves));

            // Calculate actual amount out based on the provided input amount of crs - separate tests for accuracy for this method specifically
            var amountIn = controller.GetAmountIn(amountCrsOut, expectedReserves[1], expectedReserves[0]);
            
            // Call token to Transfer from caller to Pair
            var transferFromParams = new object[] { _otherAddress, _pair, amountIn };
            SetupCall(_token, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Call pair to swap
            var swapParams = new object[] {amountCrsOut, 0ul, _otherAddress};
            SetupCall(_pair, 0, "Swap", swapParams, TransferResult.Transferred(true));
            
            // Act
            controller.SwapTokensForExactCRS(amountCrsOut, amountIn, _token, _otherAddress, 0);
            
            // Assert
            VerifyCall(_pair, 0, "GetReserves", null, Times.Once);
            VerifyCall(_token, 0, "TransferFrom", transferFromParams, Times.Once);
            VerifyCall(_pair, 0, "Swap", swapParams, Times.Once);
        }

        [Fact]
        public void SwapTokensForExactCRS_Throws_InvalidPair()
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.SwapTokensForExactCRS(1000, 1000, _token, _otherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INVALID_PAIR");
        }
        
        [Theory]
        [InlineData(6500, 2000, 200_000, 450_000)]
        public void SwapTokensForExactCRS_Throws_ExcessiveInputAmount(ulong amountCrsOut, ulong amountTokenInMax, ulong reserveToken, ulong reserveCrs)
        {
            var controller = CreateNewOpdexController();
            
            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            SetupMessage(_controller, _otherAddress);
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            SetupCall(_pair, 0, "GetReserves", null, TransferResult.Transferred(expectedReserves));
            
            controller
                .Invoking(c => c.SwapTokensForExactCRS(amountCrsOut, amountTokenInMax, _token, _otherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: EXCESSIVE_INPUT_AMOUNT");
        }
        
        #endregion

        #region Swap Exact Tokens for CRS

        [Theory]
        [InlineData(8000, 17_000, 200_000, 450_000)]
        public void SwapExactTokensForCRS_Success(ulong amountTokenIn, ulong amountCrsOutMin, ulong reserveToken, ulong reserveCrs)
        {
            // Arrange
            var controller = CreateNewOpdexController();
            
            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            SetupMessage(_controller, _otherAddress);
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            SetupCall(_pair, 0, "GetReserves", null, TransferResult.Transferred(expectedReserves));

            // Calculate actual amount out based on the provided input amount of crs - separate tests for accuracy for this method specifically
            var amountOut = controller.GetAmountOut(amountTokenIn, expectedReserves[1], expectedReserves[0]);
            
            // Call token to Transfer from caller to Pair
            var transferFromParams = new object[] { _otherAddress, _pair, amountTokenIn };
            SetupCall(_token, 0, "TransferFrom", transferFromParams, TransferResult.Transferred(true));
            
            // Call pair to swap
            var swapParams = new object[] {amountOut, 0ul, _otherAddress};
            SetupCall(_pair, 0, "Swap", swapParams, TransferResult.Transferred(true));
            
            // Act
            controller.SwapExactTokensForCRS(amountTokenIn, amountCrsOutMin, _token, _otherAddress, 0);
            
            // Assert
            VerifyCall(_pair, 0, "GetReserves", null, Times.Once);
            VerifyCall(_token, 0, "TransferFrom", transferFromParams, Times.Once);
            VerifyCall(_pair, 0, "Swap", swapParams, Times.Once);
        }

        [Fact]
        public void SwapExactTokensForCRS_Throws_InvalidPair()
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.SwapExactTokensForCRS(1000, 1000, _token, _otherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INVALID_PAIR");
        }
        
        [Theory]
        [InlineData(6500, 20000, 200_000, 450_000)]
        public void SwapExactTokensForCRS_Throws_InsufficientOutputAmount(ulong amountTokenIn, ulong amountCrsOutMin, ulong reserveToken, ulong reserveCrs)
        {
            var controller = CreateNewOpdexController();

            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            SetupMessage(_controller, _otherAddress);
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            SetupCall(_pair, 0, "GetReserves", null, TransferResult.Transferred(expectedReserves));
            
            controller
                .Invoking(c => c.SwapExactTokensForCRS(amountTokenIn, amountCrsOutMin, _token, _otherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");
        }
        
        #endregion

        #region Swap CRS for Exact Tokens

        [Theory]
        [InlineData(24_000, 10_000, 200_000, 450_000)]
        public void SwapCRSForExactTokens_Success(ulong amountCrsIn, ulong amountTokenOut, ulong reserveToken, ulong reserveCrs)
        {
            // Arrange
            var controller = CreateNewOpdexController();
            
            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            SetupMessage(_controller, _otherAddress, amountCrsIn);
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            SetupCall(_pair, 0, "GetReserves", null, TransferResult.Transferred(expectedReserves));

            var amountIn = controller.GetAmountIn(amountTokenOut, expectedReserves[0], expectedReserves[1]);

            var change = amountCrsIn - amountIn;
            
            // Transfer CRS to Pair
            SetupTransfer(_pair, amountIn, TransferResult.Transferred(true));
            
            // Call pair to swap
            var swapParams = new object[] {0ul, amountTokenOut, _otherAddress};
            SetupCall(_pair, 0, "Swap", swapParams, TransferResult.Transferred(true));

            if (change > 0)
            {
                SetupTransfer(_otherAddress, change, TransferResult.Transferred(true));
            }
            
            // Act
            controller.SwapCRSForExactTokens(amountTokenOut, _token, _otherAddress, 0);
            
            // Assert
            VerifyCall(_pair, 0, "GetReserves", null, Times.Once);
            VerifyTransfer(_pair, amountIn, Times.Once);
            VerifyCall(_pair, 0, "Swap", swapParams, Times.Once);

            if (change > 0)
            {
                VerifyTransfer(_otherAddress, change, Times.Once);
            }
        }

        [Fact]
        public void SwapCRSForExactTokens_Throws_InvalidPair()
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.SwapCRSForExactTokens(1000, _token, _otherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INVALID_PAIR");
        }
        
        [Theory]
        [InlineData(6500, 2000, 200_000, 450_000)]
        public void SwapCRSForExactTokens_Throws_ExcessiveInputAmount(ulong amountCrsIn, ulong amountTokenOut, ulong reserveToken, ulong reserveCrs)
        {
            var controller = CreateNewOpdexController();
            
            _persistentState.SetAddress($"Pair:{_token}", _pair);
            
            SetupMessage(_controller, _otherAddress);
            
            var expectedReserves = new [] { reserveCrs, reserveToken };
            SetupCall(_pair, 0, "GetReserves", null, TransferResult.Transferred(expectedReserves));
            
            controller
                .Invoking(c => c.SwapCRSForExactTokens(amountTokenOut, _token, _otherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: EXCESSIVE_INPUT_AMOUNT");
        }

        #endregion

        # region Public Helpers

        [Theory]
        [InlineData(1, 2, 3)]
        [InlineData(243, 345, 847)]
        // Todo: Precalculate expected results
        // Todo: Add more scenarios with precalculated expected results
        public void GetLiquidityQuote_Success(ulong amountA, ulong reserveA, ulong reserveB)
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
                .WithMessage("OpdexV1: INSUFFICIENT_AMOUNT");
        }
        
        [Theory]
        [InlineData(10, 1000, 0)]
        [InlineData(10, 0, 1000)]
        public void GetLiquidityQuote_Throws_InsufficientLiquidity(ulong amountA, ulong reserveA, ulong reserveB)
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.GetLiquidityQuote(amountA, reserveA, reserveB))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INSUFFICIENT_LIQUIDITY");
        }

        [Theory]
        [InlineData(1_000, 10_000, 100_000, 9_066)]
        // Todo: Add more scenarios with precalculated expected results
        public void GetAmountOut_Success(ulong amountIn, ulong reserveIn, ulong reserveOut, ulong expected)
        {
            var controller = CreateNewOpdexController();

            var amountOut = controller.GetAmountOut(amountIn, reserveIn, reserveOut);

            amountOut.Should().Be(expected);
        }
        
        [Theory]
        [InlineData(0, 10_000, 100_000)]
        public void GetAmountOut_Throws_InsufficientInputAmount(ulong amountIn, ulong reserveIn, ulong reserveOut)
        {
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.GetAmountOut(amountIn, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INSUFFICIENT_INPUT_AMOUNT");
        }
        
        [Theory]
        [InlineData(1_000, 0, 100_000)]
        [InlineData(1_000, 10_000, 0)]
        public void GetAmountOut_Throws_InsufficientLiquidity(ulong amountIn, ulong reserveIn, ulong reserveOut)
        {
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.GetAmountOut(amountIn, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INSUFFICIENT_LIQUIDITY");
        }
        
        [Theory]
        [InlineData(10_000, 10_000, 100_000, 1_115)]
        // Todo: Add more scenarios with precalculated expected results
        public void GetAmountIn_Success(ulong amountOut, ulong reserveIn, ulong reserveOut, ulong expected)
        {
            var controller = CreateNewOpdexController();

            var amountIn = controller.GetAmountIn(amountOut, reserveIn, reserveOut);

            amountIn.Should().Be(expected);
        }
        
        [Theory]
        [InlineData(0, 10_000, 100_000)]
        public void GetAmountIn_Throws_InsufficientInputAmount(ulong amountOut, ulong reserveIn, ulong reserveOut)
        {
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.GetAmountIn(amountOut, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");
        }
        
        [Theory]
        [InlineData(1_000, 0, 100_000)]
        [InlineData(1_000, 10_000, 0)]
        public void GetAmountIn_Throws_InsufficientLiquidity(ulong amountOut, ulong reserveIn, ulong reserveOut)
        {
            var controller = CreateNewOpdexController();

            controller
                .Invoking(c => c.GetAmountIn(amountOut, reserveIn, reserveOut))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INSUFFICIENT_LIQUIDITY");
        }
        
        #endregion
    }
}
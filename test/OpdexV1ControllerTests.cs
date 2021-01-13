using FluentAssertions;
using Moq;
using Stratis.SmartContracts;
using Xunit;
using Stratis.SmartContracts.CLR;

namespace OpdexV1Contracts.Tests
{
    public class OpdexV1ControllerTests
    {
        private readonly Mock<ISmartContractState> _mockContractState;
        private readonly Mock<IContractLogger> _mockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> _mockInternalExecutor;
        private readonly Address _router;
        private readonly Address _pair;
        private readonly Address _feeToSetter;
        private readonly Address _feeTo;
        private readonly Address _token;
        private readonly Address _someOtherAddress;

        public OpdexV1ControllerTests()
        {
            var mockPersistentState = new Mock<IPersistentState>();

            _mockContractLogger = new Mock<IContractLogger>();
            _mockContractState = new Mock<ISmartContractState>();
            _mockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            _mockContractState.Setup(x => x.PersistentState).Returns(mockPersistentState.Object);
            _mockContractState.Setup(x => x.ContractLogger).Returns(_mockContractLogger.Object);
            _mockContractState.Setup(x => x.InternalTransactionExecutor).Returns(_mockInternalExecutor.Object);
            _router = "0x0000000000000000000000000000000000000001".HexToAddress();
            _pair = "0x0000000000000000000000000000000000000002".HexToAddress();
            _feeToSetter = "0x0000000000000000000000000000000000000003".HexToAddress();
            _feeTo = "0x0000000000000000000000000000000000000004".HexToAddress();
            _token = "0x0000000000000000000000000000000000000005".HexToAddress();
            _someOtherAddress = "0x0000000000000000000000000000000000000006".HexToAddress();
        }

        private OpdexV1Controller CreateNewOpdexController()
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _feeToSetter, 0));
            _mockContractState.Setup(x => x.PersistentState.GetAddress("FeeToSetter")).Returns(_feeToSetter);
            _mockContractState.Setup(x => x.PersistentState.GetAddress("FeeTo")).Returns(_feeTo);

            return new OpdexV1Controller(_mockContractState.Object, _feeToSetter, _feeTo);
        }

        [Fact]
        public void CreatesNewController_Success()
        {
            var controller = CreateNewOpdexController();

            _mockContractState.Verify(x => x.PersistentState.SetAddress("FeeTo", _feeTo), Times.Once);
            _mockContractState.Verify(x => x.PersistentState.SetAddress("FeeToSetter", _feeToSetter), Times.Once);
        }

        #region FeeTo and FeeToSetter

        [Fact]
        public void SetFeeTo_Success()
        {
            var newFeeTo = _someOtherAddress;
            var controller = CreateNewOpdexController();

            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _feeToSetter, 0));

            controller.SetFeeTo(newFeeTo);

            _mockContractState.Verify(x => x.PersistentState.SetAddress("FeeTo", newFeeTo), Times.Once);
        }

        [Fact]
        public void SetFeeTo_Throws()
        {
            var newFeeTo = _someOtherAddress;
            var caller = Address.Zero; // or any other address != _feeToSetter 
            var controller = CreateNewOpdexController();

            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, caller, 0));

            controller
                .Invoking(c => c.SetFeeTo(newFeeTo))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: FORBIDDEN");
        }

        [Fact]
        public void GetFeeTo()
        {
            var controller = CreateNewOpdexController();

            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _feeToSetter, 0));
            _mockContractState.Setup(x => x.PersistentState.GetAddress("FeeTo")).Returns(_someOtherAddress);

            var feeTo = controller.GetFeeTo();

            feeTo.Should().Be(_someOtherAddress);

            _mockContractState.Verify(x => x.PersistentState.GetAddress("FeeTo"), Times.Once());
        }

        [Fact]
        public void SetFeeToSetter_Success()
        {
            var newFeeToSetter = _someOtherAddress;
            var controller = CreateNewOpdexController();

            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _feeToSetter, 0));

            controller.SetFeeToSetter(newFeeToSetter);

            _mockContractState.Verify(x => x.PersistentState.SetAddress("FeeToSetter", newFeeToSetter), Times.Once);
        }

        [Fact]
        public void SetFeeToSetter_Throws()
        {
            var caller = Address.Zero; // or any other address != _feeToSetter 
            var newFeeToSetter = _someOtherAddress;
            var controller = CreateNewOpdexController();

            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, caller, 0));

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

            _mockContractState.Setup(x => x.PersistentState.IsContract(_token)).Returns(true);
            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);

            var pair = controller.GetPair(_token);

            pair.Should().Be(_pair);

            _mockContractState.Verify(x => x.PersistentState.GetAddress($"Pair:{_token}"), Times.Once);
        }

        [Fact]
        public void CreatesPair_Success()
        {
            var controller = CreateNewOpdexController();

            var createResult = CreateResult.Succeeded(_pair);

            _mockContractState.Setup(x => x.PersistentState.IsContract(_token)).Returns(true);

            _mockInternalExecutor
                .Setup(x => x.Create<OpdexV1Pair>(_mockContractState.Object, 0, new object[] {_token}, It.IsAny<ulong>()))
                .Returns(createResult);

            var pair = controller.CreatePair(_token);
            
            _mockContractState.Verify(x => x.PersistentState.SetAddress($"Pair:{_token}", _pair), Times.Once);

            var expectedPairCreatedEvent = new OpdexV1Controller.PairCreatedEvent { Token = _token, Pair = _pair };
            _mockContractLogger.Verify(x => x.Log(_mockContractState.Object, expectedPairCreatedEvent), Times.Once);

            pair.Should().Be(_pair);
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

            _mockContractState.Setup(x => x.PersistentState.IsContract(_token)).Returns(true);
            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
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
            var to = _someOtherAddress;
            
            // Tests specific flows where there are no existing reserves
            const ulong expectedReserveCrs = 0;
            const ulong expectedReserveToken = 0;
            
            var controller = CreateNewOpdexController();

            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, amountCrsDesired));

            // Call to get reserves from pair
            var expectedReserves = new [] { expectedReserveCrs, expectedReserveToken };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedReserves));
            
            // Transfer SRC to Pair
            var transferFromParams = new object[] {_someOtherAddress, _pair, amountTokenDesired};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _token, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));
            
            // Transfer CRS to Pair
            _mockInternalExecutor
                .Setup(x => x.Transfer(_mockContractState.Object, _pair, amountCrsDesired))
                .Returns(TransferResult.Transferred(true));
            
            // Mint Liquidity Tokens
            var mintParams = new object[] {to};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "Mint", mintParams, It.IsAny<ulong>()))
                // It is not this tests responsibility to validate the minted liquidity tokens amounts
                .Returns(TransferResult.Transferred(It.IsAny<ulong>()));

            var addLiquidityResponse = controller.AddLiquidity(_token, amountTokenDesired, amountCrsMin, amountTokenMin, to, 0ul);

            addLiquidityResponse.AmountCrs.Should().Be(amountCrsDesired);
            addLiquidityResponse.AmountToken.Should().Be(amountTokenDesired);
            // It is not this tests responsibility to validate the returned minted liquidity tokens
            addLiquidityResponse.Liquidity.Should().Be(It.IsAny<ulong>());
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _token, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "Mint", mintParams, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Transfer(_mockContractState.Object, _pair, amountCrsDesired), Times.Once);
        }
        
        [Theory]
        [InlineData(1_000, 1_500, 500, 750, 100_000, 150_000)]
        [InlineData(25_000, 75_000, 20_000, 60_000, 2_500_000, 7_500_000)]
        public void AddLiquidity_Success_ExistingReserves_TokenOptimal(ulong amountCrsDesired, ulong amountTokenDesired, ulong amountCrsMin, 
            ulong amountTokenMin, ulong reserveCrs, ulong reserveToken)
        {
            var to = _someOtherAddress;
            
            var controller = CreateNewOpdexController();

            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, amountCrsDesired));

            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedReserves));
            
            // Transfer SRC to Pair
            var expectedAmountTokenOptimal = controller.GetLiquidityQuote(amountCrsDesired, reserveCrs, reserveToken);
            var transferFromParams = new object[] {_someOtherAddress, _pair, expectedAmountTokenOptimal};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _token, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));
            
            // Transfer CRS to Pair
            // TokenOptimal route always uses amountCrsDesired
            _mockInternalExecutor
                .Setup(x => x.Transfer(_mockContractState.Object, _pair, amountCrsDesired))
                .Returns(TransferResult.Transferred(true));
            
            // Mint Liquidity Tokens
            var mintParams = new object[] {to};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "Mint", mintParams, It.IsAny<ulong>()))
                // It is not this tests responsibility to validate the minted liquidity tokens amounts
                .Returns(TransferResult.Transferred(It.IsAny<ulong>()));

            var addLiquidityResponse = controller.AddLiquidity(_token, amountTokenDesired, amountCrsMin, amountTokenMin, to, 0ul);

            addLiquidityResponse.AmountCrs.Should().Be(amountCrsDesired);
            addLiquidityResponse.AmountToken.Should().Be(expectedAmountTokenOptimal);
            // It is not this tests responsibility to validate the returned minted liquidity tokens
            addLiquidityResponse.Liquidity.Should().Be(It.IsAny<ulong>());
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _token, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "Mint", mintParams, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Transfer(_mockContractState.Object, _pair, amountCrsDesired), Times.Once);
        }
        
        [Theory]
        [InlineData(1_500, 900, 750, 500, 150_000, 100_000)]
        [InlineData(75_000, 24_000, 60_000, 20_000, 7_500_000, 2_500_000)]
        public void AddLiquidity_Success_ExistingReserves_CrsOptimal(ulong amountCrsDesired, ulong amountTokenDesired, ulong amountCrsMin, 
            ulong amountTokenMin, ulong reserveCrs, ulong reserveToken)
        {
            var to = _someOtherAddress;
            
            var controller = CreateNewOpdexController();

            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, amountCrsDesired));

            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedReserves));
            
            // Transfer SRC to Pair
            // CrsOptimal route always uses amountTokenDesired
            var transferFromParams = new object[] {_someOtherAddress, _pair, amountTokenDesired};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _token, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));
            
            // Transfer CRS to Pair
            var expectedAmountCrsOptimal = controller.GetLiquidityQuote(amountTokenDesired, reserveToken, reserveCrs);
            _mockInternalExecutor
                .Setup(x => x.Transfer(_mockContractState.Object, _pair, expectedAmountCrsOptimal))
                .Returns(TransferResult.Transferred(true));
            
            // Mint Liquidity Tokens
            var mintParams = new object[] {to};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "Mint", mintParams, It.IsAny<ulong>()))
                // It is not this tests responsibility to validate the minted liquidity tokens amounts
                .Returns(TransferResult.Transferred(It.IsAny<ulong>()));
            
            // Transfer CRS change back to sender
            var change = amountCrsDesired - expectedAmountCrsOptimal;
            _mockInternalExecutor
                .Setup(x => x.Transfer(_mockContractState.Object, _someOtherAddress, change))
                .Returns(TransferResult.Transferred(true));

            var addLiquidityResponse = controller.AddLiquidity(_token, amountTokenDesired, amountCrsMin, amountTokenMin, to, 0ul);

            addLiquidityResponse.AmountCrs.Should().Be(expectedAmountCrsOptimal);
            addLiquidityResponse.AmountToken.Should().Be(amountTokenDesired);
            // It is not this tests responsibility to validate the returned minted liquidity tokens
            addLiquidityResponse.Liquidity.Should().Be(It.IsAny<ulong>());
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _token, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "Mint", mintParams, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Transfer(_mockContractState.Object, _pair, expectedAmountCrsOptimal), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Transfer(_mockContractState.Object, _someOtherAddress, amountCrsDesired - expectedAmountCrsOptimal), Times.Once);
        }
        
        #endregion

        #region Remove Liquidity

        [Theory]
        [InlineData(100, 1_000, 1_000)]
        public void RemoveLiquidity_Success(ulong liquidity, ulong amountCrsMin, ulong amountTokenMin)
        {
            var controller = CreateNewOpdexController();

            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, 0));
            
            // Transfer Liquidity tokens to pair
            var transferFromParams = new object[] {_someOtherAddress, _pair, liquidity};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));
            
            // Burn liquidity tokens
            var burnParams = new object[] {_someOtherAddress};
            var expectedBurnResponse = new [] { amountCrsMin, amountTokenMin };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "Burn", burnParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedBurnResponse));

            var removeLiquidityResponse = controller.RemoveLiquidity(_token, liquidity, amountCrsMin, amountCrsMin, _someOtherAddress, 0ul);

            removeLiquidityResponse.AmountCrs.Should().Be(amountCrsMin);
            removeLiquidityResponse.AmountToken.Should().Be(amountTokenMin);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "Burn", burnParams, It.IsAny<ulong>()), Times.Once);
        }

        [Fact]
        public void RemoveLiquidity_Throws_InvalidPair()
        {
            var controller = CreateNewOpdexController();
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, 0));
            
            controller
                .Invoking(c => c.RemoveLiquidity(_token, 100, 1000, 1000, _someOtherAddress, 0))
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

            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, 0));
            
            // Transfer Liquidity tokens to pair
            var transferFromParams = new object[] {_someOtherAddress, _pair, liquidity};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));
            
            // Burn liquidity tokens
            var burnParams = new object[] {_someOtherAddress};
            const ulong expectedAmountCrsMin = amountCrsMin - 1;
            var expectedBurnResponse = new [] { expectedAmountCrsMin, amountTokenMin };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "Burn", burnParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedBurnResponse));
            
            controller
                .Invoking(c => c.RemoveLiquidity(_token, liquidity, amountCrsMin, amountTokenMin, _someOtherAddress, 0))
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

            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, 0));
            
            // Transfer Liquidity tokens to pair
            var transferFromParams = new object[] {_someOtherAddress, _pair, liquidity};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));
            
            // Burn liquidity tokens
            var burnParams = new object[] {_someOtherAddress};
            const ulong expectedAmountTokenMin = amountTokenMin - 1;
            var expectedBurnResponse = new [] { amountCrsMin, expectedAmountTokenMin };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "Burn", burnParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedBurnResponse));
            
            controller
                .Invoking(c => c.RemoveLiquidity(_token, liquidity, amountCrsMin, amountTokenMin, _someOtherAddress, 0))
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
            
            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, amountCrsIn));
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedReserves));
            
            // Calculate actual amount out based on the provided input amount of crs - separate tests for accuracy for this method specifically
            var amountOut = controller.GetAmountOut(amountCrsIn, expectedReserves[0], expectedReserves[1]);

            // Transfer CRS to Pair
            _mockInternalExecutor
                .Setup(x => x.Transfer(_mockContractState.Object, _pair, amountCrsIn))
                .Returns(TransferResult.Transferred(true));
            
            // Call pair to swap
            var swapParams = new object[] {0ul, amountOut, _someOtherAddress};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "Swap", swapParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));
            
            // Act
            controller.SwapExactCRSForTokens(amountTokenOutMin, _token, _someOtherAddress, 0);
            
            // Assert
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Transfer(_mockContractState.Object, _pair, amountCrsIn), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "Swap", swapParams, It.IsAny<ulong>()), Times.Once);
        }

        [Fact]
        public void SwapExactCrsForTokens_Throws_InvalidPair()
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.SwapExactCRSForTokens(1000, _token, _someOtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INVALID_PAIR");
        }
        
        [Theory]
        [InlineData(6500, 14625, 200_000, 450_000)]
        public void SwapExactCrsForTokens_Throws_InsufficientOutputAmount(ulong amountTokenOutMin, ulong amountCrsIn, ulong reserveToken, ulong reserveCrs)
        {
            var controller = CreateNewOpdexController();
            
            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, amountCrsIn));
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedReserves));
            
            controller
                .Invoking(c => c.SwapExactCRSForTokens(amountTokenOutMin, _token, _someOtherAddress, 0))
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
            
            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, 0));
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedReserves));

            // Calculate actual amount out based on the provided input amount of crs - separate tests for accuracy for this method specifically
            var amountIn = controller.GetAmountIn(amountCrsOut, expectedReserves[1], expectedReserves[0]);
            
            // Call token to Transfer from caller to Pair
            var transferFromParams = new object[] { _someOtherAddress, _pair, amountIn };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _token, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));
            
            // Call pair to swap
            var swapParams = new object[] {amountCrsOut, 0ul, _someOtherAddress};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "Swap", swapParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));
            
            // Act
            controller.SwapTokensForExactCRS(amountCrsOut, amountIn, _token, _someOtherAddress, 0);
            
            // Assert
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _token, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "Swap", swapParams, It.IsAny<ulong>()), Times.Once);
        }

        [Fact]
        public void SwapTokensForExactCRS_Throws_InvalidPair()
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.SwapTokensForExactCRS(1000, 1000, _token, _someOtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INVALID_PAIR");
        }
        
        [Theory]
        [InlineData(6500, 2000, 200_000, 450_000)]
        public void SwapTokensForExactCRS_Throws_ExcessiveInputAmount(ulong amountCrsOut, ulong amountTokenInMax, ulong reserveToken, ulong reserveCrs)
        {
            var controller = CreateNewOpdexController();
            
            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, 0));
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedReserves));
            
            controller
                .Invoking(c => c.SwapTokensForExactCRS(amountCrsOut, amountTokenInMax, _token, _someOtherAddress, 0))
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
            
            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, 0));
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedReserves));

            // Calculate actual amount out based on the provided input amount of crs - separate tests for accuracy for this method specifically
            var amountOut = controller.GetAmountOut(amountTokenIn, expectedReserves[1], expectedReserves[0]);
            
            // Call token to Transfer from caller to Pair
            var transferFromParams = new object[] { _someOtherAddress, _pair, amountTokenIn };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _token, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));
            
            // Call pair to swap
            var swapParams = new object[] {amountOut, 0ul, _someOtherAddress};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "Swap", swapParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));
            
            // Act
            controller.SwapExactTokensForCRS(amountTokenIn, amountCrsOutMin, _token, _someOtherAddress, 0);
            
            // Assert
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _token, 0, "TransferFrom", transferFromParams, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "Swap", swapParams, It.IsAny<ulong>()), Times.Once);
        }

        [Fact]
        public void SwapExactTokensForCRS_Throws_InvalidPair()
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.SwapExactTokensForCRS(1000, 1000, _token, _someOtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INVALID_PAIR");
        }
        
        [Theory]
        [InlineData(6500, 20000, 200_000, 450_000)]
        public void SwapExactTokensForCRS_Throws_InsufficientOutputAmount(ulong amountTokenIn, ulong amountCrsOutMin, ulong reserveToken, ulong reserveCrs)
        {
            var controller = CreateNewOpdexController();
            
            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, 0));
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedReserves));
            
            controller
                .Invoking(c => c.SwapExactTokensForCRS(amountTokenIn, amountCrsOutMin, _token, _someOtherAddress, 0))
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
            
            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, amountCrsIn));
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedReserves));

            var amountIn = controller.GetAmountIn(amountTokenOut, expectedReserves[0], expectedReserves[1]);

            var change = amountCrsIn - amountIn;
            
            // Transfer CRS to Pair
            _mockInternalExecutor
                .Setup(x => x.Transfer(_mockContractState.Object, _pair, amountIn))
                .Returns(TransferResult.Transferred(true));
            
            // Call pair to swap
            var swapParams = new object[] {0ul, amountTokenOut, _someOtherAddress};
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "Swap", swapParams, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(true));

            if (change > 0)
            {
                _mockInternalExecutor
                    .Setup(x => x.Transfer(_mockContractState.Object, _someOtherAddress, change))
                    .Returns(TransferResult.Transferred(true));
            }
            
            // Act
            controller.SwapCRSForExactTokens(amountTokenOut, _token, _someOtherAddress, 0);
            
            // Assert
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Transfer(_mockContractState.Object, _pair, amountIn), Times.Once);
            
            _mockInternalExecutor
                .Verify(x => x.Call(_mockContractState.Object, _pair, 0, "Swap", swapParams, It.IsAny<ulong>()), Times.Once);

            if (change > 0)
            {
                _mockInternalExecutor
                    .Setup(x => x.Transfer(_mockContractState.Object, _someOtherAddress, change))
                    .Returns(TransferResult.Transferred(true));
            }
        }

        [Fact]
        public void SwapCRSForExactTokens_Throws_InvalidPair()
        {
            var controller = CreateNewOpdexController();
            
            controller
                .Invoking(c => c.SwapCRSForExactTokens(1000, _token, _someOtherAddress, 0))
                .Should().Throw<SmartContractAssertException>()
                .WithMessage("OpdexV1: INVALID_PAIR");
        }
        
        [Theory]
        [InlineData(6500, 2000, 200_000, 450_000)]
        public void SwapCRSForExactTokens_Throws_ExcessiveInputAmount(ulong amountCrsIn, ulong amountTokenOut, ulong reserveToken, ulong reserveCrs)
        {
            var controller = CreateNewOpdexController();
            
            _mockContractState.Setup(x => x.PersistentState.GetAddress($"Pair:{_token}")).Returns(_pair);
            
            _mockContractState.Setup(x => x.Message).Returns(new Message(_router, _someOtherAddress, 0));
            
            // Call to get reserves from pair
            var expectedReserves = new [] { reserveCrs, reserveToken };
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, _pair, 0, "GetReserves", null, It.IsAny<ulong>()))
                .Returns(TransferResult.Transferred(expectedReserves));
            
            controller
                .Invoking(c => c.SwapCRSForExactTokens(amountTokenOut, _token, _someOtherAddress, 0))
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
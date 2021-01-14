using System;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;

namespace OpdexV1Contracts.Tests
{
    public class BaseContractTest
    {
        private readonly Mock<ISmartContractState> _mockContractState;
        private readonly Mock<IContractLogger> _mockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> _mockInternalExecutor;
        protected readonly Address _controller;
        protected readonly Address _pair;
        protected readonly Address _feeToSetter;
        protected readonly Address _feeTo;
        protected readonly Address _token;
        protected readonly Address _trader0;
        protected readonly Address _trader1;
        protected readonly Address _otherAddress;
        protected readonly InMemoryState _persistentState;
        
        protected BaseContractTest()
        {
            _persistentState = new InMemoryState();
            _mockContractLogger = new Mock<IContractLogger>();
            _mockContractState = new Mock<ISmartContractState>();
            _mockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            _mockContractState.Setup(x => x.PersistentState).Returns(_persistentState);
            _mockContractState.Setup(x => x.ContractLogger).Returns(_mockContractLogger.Object);
            _mockContractState.Setup(x => x.InternalTransactionExecutor).Returns(_mockInternalExecutor.Object);
            _controller = "0x0000000000000000000000000000000000000001".HexToAddress();
            _pair = "0x0000000000000000000000000000000000000002".HexToAddress();
            _feeToSetter = "0x0000000000000000000000000000000000000003".HexToAddress();
            _feeTo = "0x0000000000000000000000000000000000000004".HexToAddress();
            _token = "0x0000000000000000000000000000000000000005".HexToAddress();
            _trader0 = "0x0000000000000000000000000000000000000006".HexToAddress();
            _trader1 = "0x0000000000000000000000000000000000000007".HexToAddress();
            _otherAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        }
        
        protected OpdexV1Controller CreateNewOpdexController()
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(_controller, _feeToSetter, 0));
            return new OpdexV1Controller(_mockContractState.Object, _feeToSetter, _feeTo);
        }
        
        protected OpdexV1Pair CreateNewOpdexPair()
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(_pair, _controller, 0));
            return new OpdexV1Pair(_mockContractState.Object, _token);
        }

        protected void SetupMessage(Address contractAddress, Address sender, ulong value = 0)
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(contractAddress, sender, value));
        }

        protected void SetupBalance(ulong balance)
        {
            _mockContractState.Setup(x => x.GetBalance).Returns(() => balance);
        }

        protected void SetupCall(Address to, ulong amountToTransfer, string methodName, object[] parameters, TransferResult result)
        {
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, to, amountToTransfer, methodName, parameters, It.IsAny<ulong>()))
                .Returns(result);
        }

        protected void SetupTransfer(Address to, ulong value, TransferResult result)
        {
            _mockInternalExecutor
                .Setup(x => x.Transfer(_mockContractState.Object, to, value))
                .Returns(result);
        }

        protected void SetupCreate<T>(CreateResult result, ulong amount = 0ul, object[] parameters = null)
        {
            _mockInternalExecutor
                .Setup(x => x.Create<T>(_mockContractState.Object, amount, parameters, It.IsAny<ulong>()))
                .Returns(result);
        }

        protected void VerifyCall(Address addressTo, ulong amountToTransfer, string methodName, object[] parameters, Func<Times> times)
        {
            _mockInternalExecutor.Verify(x => x.Call(_mockContractState.Object, addressTo, amountToTransfer, methodName, parameters, 0ul), times);
        }

        protected void VerifyTransfer(Address to, ulong value, Func<Times> times)
        {
            _mockInternalExecutor.Verify(x => x.Transfer(_mockContractState.Object, to, value), times);
        }

        protected void VerifyLog<T>(T expectedLog, Func<Times> times) where T : struct
        {
            _mockContractLogger.Verify(x => x.Log(_mockContractState.Object, expectedLog), times);
        }
    }
}
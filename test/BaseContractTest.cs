using System;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Networks;

namespace OpdexV1Contracts.Tests
{
    public class BaseContractTest
    {
        private readonly Mock<ISmartContractState> _mockContractState;
        private readonly Mock<IContractLogger> _mockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> _mockInternalExecutor;
        protected readonly ISerializer Serializer;
        protected readonly Address Controller;
        protected readonly Address Pair;
        protected readonly Address FeeToSetter;
        protected readonly Address FeeTo;
        protected readonly Address Token;
        protected readonly Address Trader0;
        protected readonly Address Trader1;
        protected readonly Address OtherAddress;
        protected readonly InMemoryState PersistentState;
        
        protected BaseContractTest()
        {
            PersistentState = new InMemoryState();
            _mockContractLogger = new Mock<IContractLogger>();
            _mockContractState = new Mock<ISmartContractState>();
            _mockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            Serializer = new Serializer(new ContractPrimitiveSerializer(new SmartContractsPoARegTest()));
            _mockContractState.Setup(x => x.PersistentState).Returns(PersistentState);
            _mockContractState.Setup(x => x.ContractLogger).Returns(_mockContractLogger.Object);
            _mockContractState.Setup(x => x.InternalTransactionExecutor).Returns(_mockInternalExecutor.Object);
            _mockContractState.Setup(x => x.Serializer).Returns(Serializer);
            Controller = "0x0000000000000000000000000000000000000001".HexToAddress();
            Pair = "0x0000000000000000000000000000000000000002".HexToAddress();
            FeeToSetter = "0x0000000000000000000000000000000000000003".HexToAddress();
            FeeTo = "0x0000000000000000000000000000000000000004".HexToAddress();
            Token = "0x0000000000000000000000000000000000000005".HexToAddress();
            Trader0 = "0x0000000000000000000000000000000000000006".HexToAddress();
            Trader1 = "0x0000000000000000000000000000000000000007".HexToAddress();
            OtherAddress = "0x0000000000000000000000000000000000000008".HexToAddress();
        }
        
        protected OpdexV1Controller CreateNewOpdexController(ulong balance = 0)
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(Controller, FeeToSetter, 0));
            SetupBalance(balance);
            return new OpdexV1Controller(_mockContractState.Object, FeeToSetter, FeeTo);
        }
        
        protected OpdexV1Pair CreateNewOpdexPair(ulong balance = 0)
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(Pair, Controller, 0));
            SetupBalance(balance);
            return new OpdexV1Pair(_mockContractState.Object, Token);
        }

        protected void SetupMessage(Address contractAddress, Address sender, ulong value = 0)
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(contractAddress, sender, value));
            var balance = _mockContractState.Object.GetBalance();
            SetupBalance(balance + value);
        }

        protected void SetupBalance(ulong balance)
        {
            _mockContractState.Setup(x => x.GetBalance).Returns(() => balance);
        }

        protected void SetupCall(Address to, ulong amountToTransfer, string methodName, object[] parameters, TransferResult result, Action callback = null)
        {
            _mockInternalExecutor
                .Setup(x => x.Call(_mockContractState.Object, to, amountToTransfer, methodName, parameters, It.IsAny<ulong>()))
                .Returns(result)
                .Callback(() =>
                {
                    // Adjusts for CRS sent out with a Call
                    var balance = _mockContractState.Object.GetBalance();
                    _mockContractState.Setup(x => x.GetBalance).Returns(() => checked(balance - amountToTransfer));
                    
                    // Optional callback for scenarios where CRS or SRC funds are transferred back within the call being setup ^
                    callback?.Invoke();
                });
        }

        protected void SetupTransfer(Address to, ulong value, TransferResult result)
        {
            _mockInternalExecutor
                .Setup(x => x.Transfer(_mockContractState.Object, to, value))
                .Returns(result)
                .Callback(() =>
                {
                    var balance = _mockContractState.Object.GetBalance();
                    _mockContractState.Setup(x => x.GetBalance).Returns(() => checked(balance - value));
                });
        }

        protected void SetupCreate<T>(CreateResult result, ulong amount = 0, object[] parameters = null)
        {
            _mockInternalExecutor
                .Setup(x => x.Create<T>(_mockContractState.Object, amount, parameters, It.IsAny<ulong>()))
                .Returns(result);
        }

        protected void VerifyCall(Address addressTo, ulong amountToTransfer, string methodName, object[] parameters, Func<Times> times)
        {
            _mockInternalExecutor.Verify(x => x.Call(_mockContractState.Object, addressTo, amountToTransfer, methodName, parameters, 0ul), times);
        }
        
        protected void VerifyCall(Address addressTo, ulong amountToTransfer, string methodName, object[] parameters, Times times)
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
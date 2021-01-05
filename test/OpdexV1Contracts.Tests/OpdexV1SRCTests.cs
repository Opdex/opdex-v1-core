using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;

namespace OpdexV1Contracts.Tests
{
    public class OpdexV1SRCTests
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

        public OpdexV1SRCTests()
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
    }
}
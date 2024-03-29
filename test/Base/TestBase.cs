using System;
using Moq;
using Stratis.SmartContracts;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Networks;

namespace OpdexV1Core.Tests.Base
{
    public class TestBase
    {
        private readonly Mock<ISmartContractState> _mockContractState;
        private readonly Mock<IContractLogger> _mockContractLogger;
        private readonly Mock<IInternalTransactionExecutor> _mockInternalExecutor;
        protected readonly ISerializer Serializer;
        protected readonly InMemoryState State;
        protected readonly Address Deployer;
        protected readonly Address StandardMarket;
        protected readonly Address StakingMarket;
        protected readonly Address StakingToken;
        protected readonly Address Owner;
        protected readonly Address Pool;
        protected readonly Address PoolTwo;
        protected readonly Address Token;
        protected readonly Address TokenTwo;
        protected readonly Address Trader0;
        protected readonly Address Trader1;
        protected readonly Address OtherAddress;
        protected readonly Address MiningGovernance;
        protected readonly Address MiningPool1;
        protected readonly Address MiningPool2;
        protected readonly Address MiningPool3;
        protected readonly Address MiningPool4;
        protected readonly Address MiningPool5;
        protected readonly Address Pool1;
        protected readonly Address Pool2;
        protected readonly Address Pool3;
        protected readonly Address Pool4;
        protected readonly Address Pool5;
        protected readonly Address Miner1;
        protected readonly Address Miner2;
        protected readonly Address Miner3;
        protected readonly Address Miner4;
        protected readonly Address Miner5;
        protected readonly Address Router;
        protected const ulong BlocksPerYear = 60 * 60 * 24 * 365 / 16;
        protected const ulong BlocksPerMonth = BlocksPerYear / 12;

        protected TestBase()
        {
            State = new InMemoryState();
            Serializer = new Serializer(new ContractPrimitiveSerializer(new SmartContractsPoARegTest()));
            _mockContractLogger = new Mock<IContractLogger>();
            _mockContractState = new Mock<ISmartContractState>();
            _mockInternalExecutor = new Mock<IInternalTransactionExecutor>();
            _mockContractState.Setup(x => x.PersistentState).Returns(State);
            _mockContractState.Setup(x => x.ContractLogger).Returns(_mockContractLogger.Object);
            _mockContractState.Setup(x => x.InternalTransactionExecutor).Returns(_mockInternalExecutor.Object);
            _mockContractState.Setup(x => x.Serializer).Returns(Serializer);
            Deployer = "0x0000000000000000000000000000000000000001".HexToAddress();
            StandardMarket = "0x0000000000000000000000000000000000000002".HexToAddress();
            StakingMarket = "0x0000000000000000000000000000000000000003".HexToAddress();
            StakingToken = "0x0000000000000000000000000000000000000004".HexToAddress();
            Owner = "0x0000000000000000000000000000000000000005".HexToAddress();
            Pool = "0x0000000000000000000000000000000000000006".HexToAddress();
            PoolTwo = "0x0000000000000000000000000000000000000007".HexToAddress();
            Token = "0x0000000000000000000000000000000000000008".HexToAddress();
            TokenTwo = "0x0000000000000000000000000000000000000009".HexToAddress();
            Trader0 = "0x0000000000000000000000000000000000000010".HexToAddress();
            Trader1 = "0x0000000000000000000000000000000000000011".HexToAddress();
            OtherAddress = "0x0000000000000000000000000000000000000012".HexToAddress();
            MiningGovernance = "0x0000000000000000000000000000000000000013".HexToAddress();
            MiningPool1 = "0x0000000000000000000000000000000000000014".HexToAddress();
            MiningPool2 = "0x0000000000000000000000000000000000000015".HexToAddress();
            MiningPool3 = "0x0000000000000000000000000000000000000016".HexToAddress();
            MiningPool4 = "0x0000000000000000000000000000000000000017".HexToAddress();
            MiningPool5 = "0x0000000000000000000000000000000000000018".HexToAddress();
            Pool1 = "0x0000000000000000000000000000000000000019".HexToAddress();
            Pool2 = "0x0000000000000000000000000000000000000020".HexToAddress();
            Pool3 = "0x0000000000000000000000000000000000000021".HexToAddress();
            Pool4 = "0x0000000000000000000000000000000000000022".HexToAddress();
            Pool5 = "0x0000000000000000000000000000000000000023".HexToAddress();
            Miner1 = "0x0000000000000000000000000000000000000024".HexToAddress();
            Miner2 = "0x0000000000000000000000000000000000000025".HexToAddress();
            Miner3 = "0x0000000000000000000000000000000000000026".HexToAddress();
            Miner4 = "0x0000000000000000000000000000000000000027".HexToAddress();
            Miner5 = "0x0000000000000000000000000000000000000028".HexToAddress();
            Router = "0x0000000000000000000000000000000000000029".HexToAddress();
        }

        protected IOpdexMarketDeployer CreateNewOpdexMarketDeployer()
        {
            SetupBalance(0);
            SetupBlock(10);
            SetupMessage(Deployer, Owner);
            
            SetupCreate<OpdexStakingMarket>(CreateResult.Succeeded(StakingMarket), 0ul, new object[] { StakingToken, (uint)3 });

            return new OpdexMarketDeployer(_mockContractState.Object);
        }

        protected IOpdexStakingMarket CreateNewOpdexStakingMarket(ulong balance = 0)
        {
            SetupBlock(10);
            SetupBalance(balance);
            SetupMessage(StakingMarket, Owner);
            
            return new OpdexStakingMarket(_mockContractState.Object, 3, StakingToken);
        }

        protected IOpdexStandardMarket CreateNewOpdexStandardMarket(bool authPoolCreators = false, bool authProviders = false, 
            bool authTraders = false, uint fee = 3, ulong balance = 0, bool marketFeeEnabled = false)
        {
            SetupBlock(10);
            SetupBalance(balance);
            SetupMessage(StandardMarket, Owner);
            
            return new OpdexStandardMarket(_mockContractState.Object, fee, Owner, authPoolCreators, authProviders, authTraders, marketFeeEnabled);
        }

        protected IOpdexRouter CreateNewOpdexRouter(Address market, uint marketFee, bool authProviders = false, bool authTraders = false)
        {
            SetupBlock(10);
            SetupBalance(0);
            SetupMessage(StakingMarket, Owner);
            
            return new OpdexRouter(_mockContractState.Object, market, marketFee, authProviders, authTraders);
        }

        protected IOpdexStakingPool CreateNewOpdexStakingPool(ulong balance = 0, uint fee = 3)
        {
            SetupBlock(10);
            SetupBalance(balance);
            SetupMessage(Pool, StakingMarket);
            
            State.SetContract(StakingToken, true);
            
            SetupCreate<OpdexMiningPool>(CreateResult.Succeeded(MiningPool1), 0ul, new object[] { StakingToken, Pool });

            return new OpdexStakingPool(_mockContractState.Object, Token, fee, StakingToken);
        }

        protected IOpdexStakingPool BlankStakingPool(uint fee, Address? token = null, Address? stakingToken = null)
        {
            var setToken = token ?? Token;
            var setStakingToken = stakingToken ?? StakingToken;
            
            return new OpdexStakingPool(_mockContractState.Object, setToken, fee, setStakingToken);
        }
        
        protected IOpdexStandardPool CreateNewOpdexStandardPool(ulong balance = 0, bool authProviders = false, bool authTraders = false, uint fee = 3, bool marketFeeEnabled = false)
        {
            SetupBlock(10);
            SetupBalance(balance);
            SetupMessage(Pool, StandardMarket);
            
            return new OpdexStandardPool(_mockContractState.Object, Token, fee, authProviders, authTraders, marketFeeEnabled);
        }
        
        protected IOpdexMiningPool CreateNewMiningPool(ulong block = 10)
        {
            _mockContractState.Setup(x => x.Message).Returns(new Message(MiningPool1, Pool1, 0));
            
            SetupBalance(0);
            SetupBlock(block);
            
            SetupCall(StakingToken, 0, "get_MiningGovernance", null, TransferResult.Transferred(MiningGovernance));
            SetupCall(MiningGovernance, 0, "get_MiningDuration", null, TransferResult.Transferred(BlocksPerMonth));
            
            return new OpdexMiningPool(_mockContractState.Object, StakingToken, Pool1);
        }

        protected IOpdexMiningPool BlankMiningPool()
        {
            return new OpdexMiningPool(_mockContractState.Object, StakingToken, Pool1);
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
        
        protected void SetupBlock(ulong block)
        {
            _mockContractState.Setup(x => x.Block.Number).Returns(() => block);
        }

        protected void SetupCall(Address to, ulong amountToTransfer, string methodName, object[] parameters, TransferResult result, Action callback = null)
        {
            _mockInternalExecutor
                .Setup(x => 
                    x.Call(_mockContractState.Object, to, amountToTransfer, methodName, 
                        It.Is<object[]>(p => ValidateParameters(parameters, p)), It.IsAny<ulong>()))
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
            _mockInternalExecutor.Verify(x => 
                x.Call(_mockContractState.Object, addressTo, amountToTransfer, methodName, 
                    It.Is<object[]>(p => ValidateParameters(parameters, p)), 0ul), times);
        }
        
        protected void VerifyCall(Address addressTo, ulong amountToTransfer, string methodName, object[] parameters, Times times)
        {
            _mockInternalExecutor.Verify(x => 
                x.Call(_mockContractState.Object, addressTo, amountToTransfer, methodName, 
                    It.Is<object[]>(p => ValidateParameters(parameters, p)), 0ul), times);
        }

        protected void VerifyTransfer(Address to, ulong value, Func<Times> times)
        {
            _mockInternalExecutor.Verify(x => x.Transfer(_mockContractState.Object, to, value), times);
        }

        protected void VerifyLog<T>(T expectedLog, Func<Times> times)
            where T : struct
        {
            _mockContractLogger.Verify(x => x.Log(_mockContractState.Object, expectedLog), times);
        }

        private static bool ValidateParameters(object[] expected, object[] actual)
        {
            if (expected == null && actual == null)
            {
                return true;
            }

            if (expected == null ^ actual == null)
            {
                return false;
            }

            for (var i = 0; i < expected.Length; i++)
            {
                if (expected[i].ToString() != actual[i].ToString())
                {
                    return false;
                }
            }

            return true;
        }
    }
}
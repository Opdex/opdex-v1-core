using Stratis.SmartContracts;

/// <summary>
/// 
/// </summary>
[Deploy]
public class OpdexMarketDeployer : SmartContract, IOpdexMarketDeployer
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="state"></param>
    /// <param name="stakingToken"></param>
    public OpdexMarketDeployer(ISmartContractState state, Address stakingToken) : base(state)
    {
        CreateStakingMarket(stakingToken);
    }
        
    public Address CreateStandardMarket(bool authPoolCreators, bool authProviders, bool authTraders, uint fee)
    {
        var response = Create<OpdexStandardMarket>(0ul, new object[] {authPoolCreators, authProviders, authTraders, fee});
        
        Assert(response.Success, "OPDEX: INVALID_MARKET");
        
        var market = response.NewContractAddress;

        Log(new MarketCreatedLog { Market = market });
            
        return market;
    }
        
    private void CreateStakingMarket(Address stakingToken)
    {
        const uint transactionFee = 3; // .3%

        var response = Create<OpdexStakingMarket>(0ul, new object[] {stakingToken, transactionFee});
        
        Assert(response.Success, "OPDEX: INVALID_MARKET");

        var market = response.NewContractAddress;
            
        Log(new MarketCreatedLog { Market = market });
    }
}
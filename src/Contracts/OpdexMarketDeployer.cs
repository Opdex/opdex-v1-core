using Stratis.SmartContracts;

/// <summary>
/// Deploys Opdex markets, a single staking market at the time this contract is created and
/// any number of standard, configurable markets.
/// </summary>
[Deploy]
public class OpdexMarketDeployer : SmartContract, IOpdexMarketDeployer
{
    /// <summary>
    /// Constructor initializing the contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="stakingToken">The address of the staking market's designated staking token which should be of IOpdexToken type.</param>
    public OpdexMarketDeployer(ISmartContractState state, Address stakingToken) : base(state)
    {
        CreateStakingMarket(stakingToken);
    }
        
    public Address CreateStandardMarket(bool authPoolCreators, bool authProviders, bool authTraders, uint fee)
    {
        var response = Create<OpdexStandardMarket>(0, new object[] {Message.Sender, authPoolCreators, authProviders, authTraders, fee});
        
        Assert(response.Success, "OPDEX: INVALID_MARKET");
        
        var market = response.NewContractAddress;

        Log(new MarketCreatedLog
        {
            Market = market, 
            AuthPoolCreators = authPoolCreators, 
            AuthProviders = authProviders, 
            AuthTraders = authTraders, 
            Fee = fee,
            StakingToken = Address.Zero
        });
            
        return market;
    }
        
    private void CreateStakingMarket(Address stakingToken)
    {
        const uint transactionFee = 3; // .3% for the staking market

        var response = Create<OpdexStakingMarket>(0, new object[] {stakingToken, transactionFee});
        
        Assert(response.Success, "OPDEX: INVALID_MARKET");

        var market = response.NewContractAddress;
            
        Log(new MarketCreatedLog
        {
            Market = market, 
            AuthPoolCreators = false, 
            AuthProviders = false, 
            AuthTraders = false, 
            Fee = transactionFee,
            StakingToken = stakingToken
        });
    }
}
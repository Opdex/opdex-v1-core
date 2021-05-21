using Stratis.SmartContracts;

/// <summary>
/// Deploys Opdex markets, a single staking market at the time this contract is created and
/// any number of standard, configurable markets.
/// </summary>
[Deploy]
public class OpdexMarketDeployer : SmartContract, IOpdexMarketDeployer
{
    private const string UnauthorizedError = "OPDEX: UNAUTHORIZED";
    private const string InvalidMarketError = "OPDEX: INVALID_MARKET";
    
    /// <summary>
    /// Constructor initializing the contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="stakingToken">The address of the staking market's designated staking token which should be of IOpdexToken type.</param>
    public OpdexMarketDeployer(ISmartContractState state, Address stakingToken) : base(state)
    {
        Owner = Message.Sender;
        CreateStakingMarket(stakingToken);
    }

    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }
    
    /// <inheritdoc />
    public void SetOwner(Address address)
    {
        Assert(Message.Sender == Owner, UnauthorizedError);
        
        Owner = address;
        
        Log(new ChangeDeployerOwnerLog {From = Message.Sender, To = address});
    }
    
    /// <inheritdoc />
    public Address CreateStandardMarket(Address marketOwner, bool authPoolCreators, bool authProviders, bool authTraders, uint fee)
    {
        Assert(Message.Sender == Owner, UnauthorizedError);
        
        var response = Create<OpdexStandardMarket>(0, new object[] {marketOwner, authPoolCreators, authProviders, authTraders, fee});
        
        Assert(response.Success, InvalidMarketError);
        
        var market = response.NewContractAddress;

        Log(new CreateMarketLog
        {
            Market = market, 
            Owner = marketOwner,
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
        
        Assert(response.Success, InvalidMarketError);

        var market = response.NewContractAddress;
            
        Log(new CreateMarketLog
        {
            Market = market, 
            Owner = Message.Sender,
            AuthPoolCreators = false, 
            AuthProviders = false, 
            AuthTraders = false, 
            Fee = transactionFee,
            StakingToken = stakingToken
        });
    }
}
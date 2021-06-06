using Stratis.SmartContracts;

/// <summary>
/// Deploys Opdex markets of staking or standard types with an individual router contract per market.
/// </summary>
[Deploy]
public class OpdexMarketDeployer : SmartContract, IOpdexMarketDeployer
{
    /// <summary>
    /// Constructor initializing the market deployer contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    public OpdexMarketDeployer(ISmartContractState state) : base(state)
    {
        Owner = Message.Sender;
    }

    /// <inheritdoc />
    public Address Owner
    {
        get => State.GetAddress(DeployerStateKeys.Owner);
        private set => State.SetAddress(DeployerStateKeys.Owner, value);
    }

    /// <inheritdoc />
    public void SetOwner(Address address)
    {
        EnsureOwnerOnly();

        Owner = address;

        Log(new ChangeDeployerOwnerLog {From = Message.Sender, To = address});
    }

    /// <inheritdoc />
    public Address CreateStandardMarket(Address marketOwner, uint transactionFee, bool authPoolCreators, bool authProviders, bool authTraders, bool enableMarketFee)
    {
        EnsureOwnerOnly();

        // Temporarily set this contract as owner if the router needs permissions
        var ownerToSet = authProviders || authTraders ? Address : marketOwner;

        var marketParams = new object[] {transactionFee, ownerToSet, authPoolCreators, authProviders, authTraders, enableMarketFee};
        var marketResponse = Create<OpdexStandardMarket>(0, marketParams);

        Assert(marketResponse.Success, "OPDEX: INVALID_MARKET");

        var market = marketResponse.NewContractAddress;
        var router = CreateOpdexRouter(market, transactionFee, authProviders, authTraders);

        // Give the router provide permissions if necessary
        if (authProviders) AuthRouter(market, router, Permissions.Provide);

        // Give the router trade permissions if necessary
        if (authTraders) AuthRouter(market, router, Permissions.Trade);

        // Replace this contract as the market owner with the intended market owner if necessary
        if (ownerToSet != marketOwner)
        {
            var setOwnerResponse = Call(market, 0, nameof(IOpdexStandardMarket.SetOwner), new object[] { marketOwner });
            Assert(setOwnerResponse.Success, "OPDEX: SET_OWNER_FAILURE");
        }

        Log(new CreateMarketLog
        {
            Market = market,
            Owner = marketOwner,
            Router = router,
            AuthPoolCreators = authPoolCreators,
            AuthProviders = authProviders,
            AuthTraders = authTraders,
            TransactionFee = transactionFee,
            MarketFeeEnabled = enableMarketFee
        });

        return market;
    }

    /// <inheritdoc />
    public Address CreateStakingMarket(Address stakingToken)
    {
        EnsureOwnerOnly();

        const uint transactionFee = 3; // .3% for the staking market

        Assert(State.IsContract(stakingToken), "OPDEX: INVALID_STAKING_TOKEN");

        var marketParams = new object[] {transactionFee, stakingToken};
        var marketResponse = Create<OpdexStakingMarket>(0, marketParams);

        Assert(marketResponse.Success, "OPDEX: INVALID_MARKET");

        var market = marketResponse.NewContractAddress;
        var router = CreateOpdexRouter(market, transactionFee, false, false);

        Log(new CreateMarketLog
        {
            Market = market,
            Owner = Message.Sender,
            Router = router,
            TransactionFee = transactionFee,
            StakingToken = stakingToken
        });

        return market;
    }

    private Address CreateOpdexRouter(Address market, uint transactionFee, bool authProviders, bool authTraders)
    {
        var routerResponse = Create<OpdexRouter>(0, new object[] {market, transactionFee, authProviders, authTraders});

        Assert(routerResponse.Success, "OPDEX: INVALID_ROUTER");

        return routerResponse.NewContractAddress;
    }

    private void AuthRouter(Address market, Address router, Permissions permission)
    {
        const bool authorize = true;
        var authRouterResponse = Call(market, 0, nameof(IOpdexStandardMarket.Authorize), new object[] { router, (byte)permission, authorize });
        Assert(authRouterResponse.Success, "OPDEX: AUTH_ROUTER_FAILURE");
    }

    private void EnsureOwnerOnly()
    {
        Assert(Message.Sender == Owner, "OPDEX: UNAUTHORIZED");
    }
}
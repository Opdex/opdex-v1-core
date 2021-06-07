using Stratis.SmartContracts;

/// <summary>
/// Staking market contract used for managing available staking pools.
/// </summary>
public class OpdexStakingMarket : OpdexMarket, IOpdexStakingMarket
{
    /// <summary>
    /// Constructor initializing the staking market.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="transactionFee">The market transaction fee, 0-10 equal to 0-1%.</param>
    /// <param name="stakingToken">The address of the staking token.</param>
    public OpdexStakingMarket(ISmartContractState state, uint transactionFee, Address stakingToken) : base(state, transactionFee)
    {
        StakingToken = stakingToken;
    }

    /// <inheritdoc />
    public Address StakingToken
    {
        get => State.GetAddress(MarketStateKeys.StakingToken);
        private set => State.SetAddress(MarketStateKeys.StakingToken, value);
    }

    /// <inheritdoc />
    public override Address CreatePool(Address token)
    {
        Assert(State.IsContract(token), "OPDEX: INVALID_TOKEN");

        var pool = GetPool(token);

        Assert(pool == Address.Zero, "OPDEX: POOL_EXISTS");

        var poolResponse = Create<OpdexStakingPool>(0, new object[] {token, TransactionFee, StakingToken});

        Assert(poolResponse.Success, "OPDEX: INVALID_POOL");

        pool = poolResponse.NewContractAddress;

        SetPool(token, pool);

        Log(new CreateLiquidityPoolLog { Token = token, Pool = pool });

        return pool;
    }
}
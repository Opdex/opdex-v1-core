using Stratis.SmartContracts;

/// <summary>
/// Staking market contract used for managing available staking pools and routing transactions. Validates and completes prerequisite
/// transactions necessary for adding or removing liquidity or swapping in liquidity pools.
/// </summary>
public class OpdexStakingMarket : OpdexMarket, IOpdexStakingMarket
{
    /// <summary>
    /// Constructor initializing the staking market.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="stateToken">The address of the staking token.</param>
    /// <param name="fee">The market transaction fee, 0-10 equal to 0-1%.</param>
    public OpdexStakingMarket(ISmartContractState state, Address stateToken, uint fee) : base(state, fee)
    {
        StakeToken = stateToken;
    }

    /// <inheritdoc />
    public Address StakeToken
    {
        get => State.GetAddress(nameof(StakeToken));
        private set => State.SetAddress(nameof(StakeToken), value);
    }
        
    /// <inheritdoc />
    public override Address CreatePool(Address token)
    {
        Assert(token != Address.Zero && State.IsContract(token), "OPDEX: ZERO_ADDRESS");
        
        var pool = GetPool(token);
        
        Assert(pool == Address.Zero, "OPDEX: POOL_EXISTS");
        
        var poolResponse = Create<OpdexStakingPool>(0, new object[] {token, StakeToken, Fee});
        
        Assert(poolResponse.Success, "OPDEX: INVALID_POOL");

        pool = poolResponse.NewContractAddress;
        
        SetPool(token, pool);
        
        Log(new LiquidityPoolCreatedLog { Token = token, Pool = pool });
        
        return pool;
    }
}
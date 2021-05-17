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
    /// <param name="stakingToken">The address of the staking token.</param>
    /// <param name="fee">The market transaction fee, 0-10 equal to 0-1%, Market Deploy hard-codes 3.</param>
    public OpdexStakingMarket(ISmartContractState state, Address stakingToken, uint fee) : base(state, fee)
    {
        StakingToken = stakingToken;
    }

    /// <inheritdoc />
    public Address StakingToken
    {
        get => State.GetAddress(nameof(StakingToken));
        private set => State.SetAddress(nameof(StakingToken), value);
    }
        
    /// <inheritdoc />
    public override Address CreatePool(Address token)
    {
        Assert(State.IsContract(token), "OPDEX: INVALID_TOKEN");
        
        var pool = GetPool(token);
        
        Assert(pool == Address.Zero, "OPDEX: POOL_EXISTS");
        
        var poolResponse = Create<OpdexStakingPool>(0, new object[] {token, StakingToken, Fee});
        
        Assert(poolResponse.Success, "OPDEX: INVALID_POOL");

        pool = poolResponse.NewContractAddress;
        
        SetPool(token, pool);
        
        Log(new CreateLiquidityPoolLog { Token = token, Pool = pool });
        
        return pool;
    }
}
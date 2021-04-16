using Stratis.SmartContracts;

/// <summary>
/// 
/// </summary>
public class OpdexStakingMarket : OpdexMarket, IOpdexStakingMarket
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="state"></param>
    /// <param name="stateToken"></param>
    /// <param name="fee"></param>
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
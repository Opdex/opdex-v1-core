using Stratis.SmartContracts;

/// <summary>
/// Standard market contract used for managing available pools and routing transactions. Validates and completes prerequisite
/// transactions necessary for adding or removing liquidity or swapping in liquidity pools. Optionally requires a whitelist
/// for liquidity providing, creating pools, or swaps.
/// </summary>
public class OpdexStandardMarket : OpdexMarket, IOpdexStandardMarket
{
    /// <summary>
    /// Constructor to initialize a standard market.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="owner">The owner of the market.</param>
    /// <param name="authPoolCreators">Flag to authorize liquidity pool creators.</param>
    /// <param name="authProviders">Flag to authorize liquidity pool providers.</param>
    /// <param name="authTraders">Flag to authorize traders.</param>
    /// <param name="fee">The market transaction fee, 0-10 equal to 0-1%.</param>
    public OpdexStandardMarket(
        ISmartContractState state, 
        Address owner,
        bool authPoolCreators, 
        bool authProviders, 
        bool authTraders, 
        uint fee) : base(state, fee)
    {
        AuthPoolCreators = authPoolCreators;
        AuthProviders = authProviders;
        AuthTraders = authTraders;
        Owner = owner;
    }
    
    /// <inheritdoc />
    public bool AuthTraders
    {
        get => State.GetBool(nameof(AuthTraders));
        private set => State.SetBool(nameof(AuthTraders), value);
    }
        
    /// <inheritdoc />
    public bool AuthProviders
    {
        get => State.GetBool(nameof(AuthProviders));
        private set => State.SetBool(nameof(AuthProviders), value);
    }
    
    /// <inheritdoc />
    public bool AuthPoolCreators
    {
        get => State.GetBool(nameof(AuthPoolCreators));
        private set => State.SetBool(nameof(AuthPoolCreators), value);
    }

    /// <inheritdoc />
    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }

    /// <inheritdoc />
    public bool IsAuthorizedFor(Address address, byte permission)
    {
        switch ((Permissions)permission)
        {
            case Permissions.Trade when !AuthTraders:
            case Permissions.Provide when !AuthProviders:
            case Permissions.CreatePool when !AuthPoolCreators: return true;
            case Permissions.Unknown: return false;
            default: return address == Owner || State.GetBool($"AuthorizedFor:{permission}:{address}");
        }
    }

    /// <inheritdoc />
    public void Authorize(Address address, byte permission, bool authorize)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.SetPermissions);
        
        Assert((Permissions)permission != Permissions.Unknown, "OPDEX: INVALID_PERMISSION");
        
        State.SetBool($"AuthorizedFor:{permission}:{address}", authorize);
        
        Log(new PermissionsChangeLog { Address = address, Permission = permission, IsAuthorized = authorize });
    }

    /// <inheritdoc />
    public void SetOwner(Address address)
    {
        Assert(Message.Sender == Owner, "OPDEX: UNAUTHORIZED");
        
        Owner = address;
        
        Log(new MarketOwnerChangeLog {From = Message.Sender, To = address});
    }
    
    /// <inheritdoc />
    public void SetPoolMarket(Address token, Address newMarket)
    {
        Assert(Message.Sender == Owner, "OPDEX: UNAUTHORIZED");

        var isAuthorizedParams = new object[] {Message.Sender, (byte)Permissions.SetPermissions};
        var isAuthorizedResponse = Call(newMarket, 0, nameof(IOpdexStandardMarket.IsAuthorizedFor), isAuthorizedParams);
        
        Assert(isAuthorizedResponse.Success && (bool)isAuthorizedResponse.ReturnValue, "OPDEX: INVALID_MARKET");
        
        var pool = GetValidatedPool(token);

        var updatePoolResponse = Call(pool, 0, nameof(IOpdexStandardPool.SetMarket), new object[] {newMarket});
        
        Assert(updatePoolResponse.Success, "OPDEX: CHANGE_MARKET_FAILED");
    }

    /// <inheritdoc />
    public override Address CreatePool(Address token)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.CreatePool);
        
        Assert(State.IsContract(token), "OPDEX: INVALID_TOKEN");
        
        var pool = GetPool(token);
        
        Assert(pool == Address.Zero, "OPDEX: POOL_EXISTS");
        
        var poolResponse = Create<OpdexStandardPool>(0, new object[] {token, AuthProviders, AuthTraders, Fee});

        Assert(poolResponse.Success, "OPDEX: INVALID_POOL");

        pool = poolResponse.NewContractAddress;
        
        SetPool(token, pool);
        
        Log(new LiquidityPoolCreatedLog { Token = token, Pool = pool });
        
        return pool;
    }
    
    /// <inheritdoc />
    public override UInt256[] AddLiquidity(Address token, UInt256 amountSrcDesired, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline)
    { 
        EnsureAuthorizationFor(Message.Sender, Permissions.Provide);
        
        return AddLiquidityExecute(token, amountSrcDesired, amountCrsMin, amountSrcMin, to, deadline);
    }
    
    /// <inheritdoc />
    public override UInt256[] RemoveLiquidity(Address token, UInt256 liquidity, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Provide);
        
        return RemoveLiquidityExecute(token, liquidity, amountCrsMin, amountSrcMin, to, deadline);
    }
    
    /// <inheritdoc />
    public override UInt256 SwapExactCrsForSrc(UInt256 amountSrcOutMin, Address token, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Trade);
        
        return SwapExactCrsForSrcExecute(amountSrcOutMin, token, to, deadline);
    }
    
    /// <inheritdoc />
    public override UInt256 SwapSrcForExactCrs(ulong amountCrsOut, UInt256 amountSrcInMax, Address token, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Trade);
        
        return SwapSrcForExactCrsExecute(amountCrsOut, amountSrcInMax, token, to, deadline);
    }
    
    /// <inheritdoc />
    public override ulong SwapExactSrcForCrs(UInt256 amountSrcIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Trade);
        
        return SwapExactSrcForCrsExecute(amountSrcIn, amountCrsOutMin, token, to, deadline);
    }
    
    /// <inheritdoc />
    public override ulong SwapCrsForExactSrc(UInt256 amountSrcOut, Address token, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Trade);
        
        return SwapCrsForExactSrcExecute(amountSrcOut, token, to, deadline);
    }

    /// <inheritdoc />
    public override UInt256 SwapSrcForExactSrc(UInt256 amountSrcInMax, Address tokenIn, UInt256 amountSrcOut, Address tokenOut, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Trade);
        
        return SwapSrcForExactSrcExecute(amountSrcInMax, tokenIn, amountSrcOut, tokenOut, to, deadline);
    }
    
    /// <inheritdoc />
    public override UInt256 SwapExactSrcForSrc(UInt256 amountSrcIn, Address tokenIn, UInt256 amountSrcOutMin, Address tokenOut, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Trade);
        
        return SwapExactSrcForSrcExecute(amountSrcIn, tokenIn, amountSrcOutMin, tokenOut, to, deadline);
    }
    
    private void EnsureAuthorizationFor(Address address, Permissions permission)
    {
        Assert(IsAuthorizedFor(address, (byte)permission), "OPDEX: UNAUTHORIZED");
    }
}

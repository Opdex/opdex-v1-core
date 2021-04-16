using Stratis.SmartContracts;

/// <summary>
/// Controller contract used for managing available pools and routing transactions. Validates and completes prerequisite
/// transactions necessary for adding or removing liquidity or swapping in liquidity pools.
/// </summary>
public class OpdexStandardMarket : OpdexMarket, IOpdexStandardMarket
{
    /// <summary>
    /// Constructor to initialize the controller.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="owner"></param>
    /// <param name="authPoolCreators"></param>
    /// <param name="authProviders"></param>
    /// <param name="authTraders"></param>
    /// <param name="fee"></param>
    public OpdexStandardMarket(
        ISmartContractState state, 
        Address owner,
        bool authPoolCreators, 
        bool authProviders, 
        bool authTraders, 
        uint fee) : base(state, fee)
    {
        AuthorizePoolCreators = authPoolCreators;
        AuthorizeProviders = authProviders;
        AuthorizeTraders = authTraders;
        Owner = owner;
    }
    
    /// <inheritdoc />
    public bool AuthorizeTraders
    {
        get => State.GetBool(nameof(AuthorizeTraders));
        private set => State.SetBool(nameof(AuthorizeTraders), value);
    }
        
    /// <inheritdoc />
    public bool AuthorizeProviders
    {
        get => State.GetBool(nameof(AuthorizeProviders));
        private set => State.SetBool(nameof(AuthorizeProviders), value);
    }
    
    /// <inheritdoc />
    public bool AuthorizePoolCreators
    {
        get => State.GetBool(nameof(AuthorizePoolCreators));
        private set => State.SetBool(nameof(AuthorizePoolCreators), value);
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
            case Permissions.Trade when !AuthorizeTraders:
            case Permissions.Provide when !AuthorizeProviders:
            case Permissions.CreatePool when !AuthorizePoolCreators:
                return true;
            default:
                return address == Owner || State.GetBool($"AuthorizedFor:{permission}:{address}");
        }
    }
    
    private void EnsureAuthorizationFor(Address address, Permissions permission)
    {
        Assert(IsAuthorizedFor(address, (byte)permission), "OPDEX: UNAUTHORIZED");
    }

    private void SetAuthorizationFor(Address address, Permissions permission, bool isAuthorized)
    {
        State.SetBool($"AuthorizedFor:{permission}:{address}", isAuthorized);
    }

    /// <inheritdoc />
    public void Authorize(Address address, byte permission, bool isAuthorized)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.SetPermissions);
        
        Assert((Permissions)permission != Permissions.Unknown);
        
        SetAuthorizationFor(address, (Permissions)permission, isAuthorized);
    }

    /// <inheritdoc />
    public void SetOwner(Address address)
    {
        Assert(Message.Sender == Owner, "OPDEX: UNAUTHORIZED");
        
        Owner = address;

        Log(new MarketOwnerChangeLog {From = Message.Sender, To = address});
    }

    /// <inheritdoc />
    public override Address CreatePool(Address token)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.CreatePool);
        
        Assert(token != Address.Zero && State.IsContract(token), "OPDEX: ZERO_ADDRESS");
        
        var pool = GetPool(token);
        
        Assert(pool == Address.Zero, "OPDEX: POOL_EXISTS");
        
        var poolResponse = Create<OpdexStandardPool>(0, new object[] {token, AuthorizeProviders, AuthorizeTraders, Fee});

        Assert(poolResponse.Success, "OPDEX: INVALID_POOL");

        pool = poolResponse.NewContractAddress;
        
        SetPool(token, pool);
        
        Log(new LiquidityPoolCreatedLog { Token = token, Pool = pool });
        
        return pool;
    }
    
    /// <inheritdoc />
    public override object[] AddLiquidity(Address token, UInt256 amountSrcDesired, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline)
    { 
        EnsureAuthorizationFor(Message.Sender, Permissions.Provide);
        EnsureAuthorizationFor(to, Permissions.Provide);
        
        return AddLiquidityExecute(token, amountSrcDesired, amountCrsMin, amountSrcMin, to, deadline);
    }
    
    /// <inheritdoc />
    public override object[] RemoveLiquidity(Address token, UInt256 liquidity, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Provide);
        EnsureAuthorizationFor(to, Permissions.Provide);
        
        return RemoveLiquidityExecute(token, liquidity, amountCrsMin, amountSrcMin, to, deadline);
    }
    
    /// <inheritdoc />
    public override void SwapExactCrsForSrc(UInt256 amountSrcOutMin, Address token, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Trade);
        SwapExactCrsForSrcExecute(amountSrcOutMin, token, to, deadline);
    }
    
    /// <inheritdoc />
    public override void SwapSrcForExactCrs(ulong amountCrsOut, UInt256 amountSrcInMax, Address token, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Trade);
        SwapSrcForExactCrsExecute(amountCrsOut, amountSrcInMax, token, to, deadline);
    }
    
    /// <inheritdoc />
    public override void SwapExactSrcForCrs(UInt256 amountSrcIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Trade);
        SwapExactSrcForCrsExecute(amountSrcIn, amountCrsOutMin, token, to, deadline);
    }
    
    /// <inheritdoc />
    public override void SwapCrsForExactSrc(UInt256 amountSrcOut, Address token, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Trade);
        SwapCrsForExactSrcExecute(amountSrcOut, token, to, deadline);
    }

    /// <inheritdoc />
    public override void SwapSrcForExactSrc(UInt256 amountSrcInMax, Address tokenIn, UInt256 amountSrcOut, Address tokenOut, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Trade);
        SwapSrcForExactSrcExecute(amountSrcInMax, tokenIn, amountSrcOut, tokenOut, to, deadline);
    }
    
    /// <inheritdoc />
    public override void SwapExactSrcForSrc(UInt256 amountSrcIn, Address tokenIn, UInt256 amountSrcOutMin, Address tokenOut, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.Trade);
        SwapExactSrcForSrcExecute(amountSrcIn, tokenIn, amountSrcOutMin, tokenOut, to, deadline);
    }
}

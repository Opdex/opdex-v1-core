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
    public bool IsAuthorized(Address address, byte permission)
    {
        switch ((Permissions)permission)
        {
            case Permissions.Trade when !AuthTraders:
            case Permissions.Provide when !AuthProviders:
            case Permissions.CreatePool when !AuthPoolCreators: return true;
            case Permissions.Unknown: return false;
            default: return address == Owner || State.GetBool($"IsAuthorized:{permission}:{address}");
        }
    }

    /// <inheritdoc />
    public bool IsAuthorized(Address sender, Address receiver, byte permission)
    {
        return IsAuthorized(sender, permission) && IsAuthorized(receiver, permission);
    }

    /// <inheritdoc />
    public void Authorize(Address address, byte permission, bool authorize)
    {
        EnsureAuthorizationFor(Message.Sender, Permissions.SetPermissions);
        
        Assert((Permissions)permission != Permissions.Unknown, "OPDEX: INVALID_PERMISSION");
        
        State.SetBool($"IsAuthorized:{permission}:{address}", authorize);
        
        Log(new ChangeMarketPermissionLog { Address = address, Permission = permission, IsAuthorized = authorize });
    }

    /// <inheritdoc />
    public void SetOwner(Address address)
    {
        Assert(Message.Sender == Owner, "OPDEX: UNAUTHORIZED");
        
        Owner = address;
        
        Log(new ChangeMarketOwnerLog {From = Message.Sender, To = address});
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
        
        Log(new CreateLiquidityPoolLog { Token = token, Pool = pool });
        
        return pool;
    }
    
    private void EnsureAuthorizationFor(Address address, Permissions permission)
    {
        Assert(IsAuthorized(address, (byte)permission), "OPDEX: UNAUTHORIZED");
    }
}

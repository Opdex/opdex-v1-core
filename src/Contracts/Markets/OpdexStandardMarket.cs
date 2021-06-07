using System;
using Stratis.SmartContracts;

/// <summary>
/// Standard market contract used for managing available pools and optionally permissions for market access.
/// </summary>
public class OpdexStandardMarket : OpdexMarket, IOpdexStandardMarket
{
    /// <summary>
    /// Constructor to initialize a standard market.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="transactionFee">The market transaction fee, 0-10 equal to 0-1%.</param>
    /// <param name="owner">The owner of the market.</param>
    /// <param name="authPoolCreators">Flag to authorize liquidity pool creators.</param>
    /// <param name="authProviders">Flag to authorize liquidity pool providers.</param>
    /// <param name="authTraders">Flag to authorize traders.</param>
    /// <param name="enableMarketFee">Flag to determine if 1/6 of transaction fees should be collected by the market.</param>
    public OpdexStandardMarket(
        ISmartContractState state,
        uint transactionFee,
        Address owner,
        bool authPoolCreators,
        bool authProviders,
        bool authTraders,
        bool enableMarketFee) : base(state, transactionFee)
    {
        if (transactionFee == 0) Assert(!enableMarketFee, "OPDEX: INVALID_MARKET_FEE");

        AuthPoolCreators = authPoolCreators;
        AuthProviders = authProviders;
        AuthTraders = authTraders;
        Owner = owner;
        MarketFeeEnabled = enableMarketFee;
    }

    /// <inheritdoc />
    public bool AuthTraders
    {
        get => State.GetBool(MarketStateKeys.AuthTraders);
        private set => State.SetBool(MarketStateKeys.AuthTraders, value);
    }

    /// <inheritdoc />
    public bool AuthProviders
    {
        get => State.GetBool(MarketStateKeys.AuthProviders);
        private set => State.SetBool(MarketStateKeys.AuthProviders, value);
    }

    /// <inheritdoc />
    public bool AuthPoolCreators
    {
        get => State.GetBool(MarketStateKeys.AuthPoolCreators);
        private set => State.SetBool(MarketStateKeys.AuthPoolCreators, value);
    }

    /// <inheritdoc />
    public bool MarketFeeEnabled
    {
        get => State.GetBool(MarketStateKeys.MarketFeeEnabled);
        private set => State.SetBool(MarketStateKeys.MarketFeeEnabled, value);
    }

    /// <inheritdoc />
    public Address Owner
    {
        get => State.GetAddress(MarketStateKeys.Owner);
        private set => State.SetAddress(MarketStateKeys.Owner, value);
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
            default: return address == Owner || State.GetBool($"{MarketStateKeys.IsAuthorized}:{permission}:{address}");
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

        // permission != 0 && permission <= 4
        Assert((Permissions)permission != Permissions.Unknown && permission <= (byte)Permissions.SetPermissions, "OPDEX: INVALID_PERMISSION");

        State.SetBool($"{MarketStateKeys.IsAuthorized}:{permission}:{address}", authorize);

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

        var poolResponse = Create<OpdexStandardPool>(0, new object[] {token, TransactionFee, AuthProviders, AuthTraders, MarketFeeEnabled});

        Assert(poolResponse.Success, "OPDEX: INVALID_POOL");

        pool = poolResponse.NewContractAddress;

        SetPool(token, pool);

        Log(new CreateLiquidityPoolLog { Token = token, Pool = pool });

        return pool;
    }

    /// <inheritdoc />
    public void CollectMarketFees(Address token, UInt256 amount)
    {
        if (amount == 0 || !MarketFeeEnabled) return;

        var owner = Owner;

        Assert(Message.Sender == owner, "OPDEX: UNAUTHORIZED");

        var pool = GetPool(token);

        Assert(pool != Address.Zero, "OPDEX: INVALID_POOL");

        SafeTransferTo(pool, owner, amount);
    }

    private void EnsureAuthorizationFor(Address address, Permissions permission)
    {
        Assert(IsAuthorized(address, (byte)permission), "OPDEX: UNAUTHORIZED");
    }

    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;

        var result = Call(token, 0, nameof(IOpdexLiquidityPool.TransferTo), new object[] {to, amount});

        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }
}

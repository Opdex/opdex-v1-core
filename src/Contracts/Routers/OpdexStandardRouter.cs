using Stratis.SmartContracts;

/// <summary>
/// 
/// </summary>
public class OpdexStandardRouter : OpdexRouter, IOpdexStandardRouter
{
    private const string AuthProvidersGetter = "get_AuthProviders";
    private const string AuthTradersGetter = "get_AuthTraders";
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="state"></param>
    /// <param name="market"></param>
    public OpdexStandardRouter(ISmartContractState state, Address market) : base(state, market)
    {
        AuthProviders = GetAuthFlag(AuthProvidersGetter);
        AuthTraders = GetAuthFlag(AuthTradersGetter);
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
    public override UInt256[] AddLiquidity(Address token, UInt256 amountSrcDesired, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Provide);
        return AddLiquidityExecute(token, amountSrcDesired, amountCrsMin, amountSrcMin, to, deadline);
    }

    /// <inheritdoc />
    public override UInt256[] RemoveLiquidity(Address token, UInt256 liquidity, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Provide);
        return RemoveLiquidityExecute(token, liquidity, amountCrsMin, amountSrcMin, to, deadline);
    }

    /// <inheritdoc />
    public override UInt256 SwapExactCrsForSrc(UInt256 amountSrcOutMin, Address token, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Trade);
        return SwapExactCrsForSrcExecute(amountSrcOutMin, token, to, deadline);
    }

    /// <inheritdoc />
    public override UInt256 SwapSrcForExactCrs(ulong amountCrsOut, UInt256 amountSrcInMax, Address token, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Trade);
        return SwapSrcForExactCrsExecute(amountCrsOut, amountSrcInMax, token, to, deadline);
    }

    /// <inheritdoc />
    public override ulong SwapExactSrcForCrs(UInt256 amountSrcIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Trade);
        return SwapExactSrcForCrsExecute(amountSrcIn, amountCrsOutMin, token, to, deadline);
    }

    /// <inheritdoc />
    public override ulong SwapCrsForExactSrc(UInt256 amountSrcOut, Address token, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Trade);
        return SwapCrsForExactSrcExecute(amountSrcOut, token, to, deadline);
    }

    /// <inheritdoc />
    public override UInt256 SwapSrcForExactSrc(UInt256 amountSrcInMax, Address tokenIn, UInt256 amountSrcOut, Address tokenOut, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Trade);
        return SwapSrcForExactSrcExecute(amountSrcInMax, tokenIn, amountSrcOut, tokenOut, to, deadline);
    }

    /// <inheritdoc />
    public override UInt256 SwapExactSrcForSrc(UInt256 amountSrcIn, Address tokenIn, UInt256 amountSrcOutMin, Address tokenOut, Address to, ulong deadline)
    {
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Trade);
        return SwapExactSrcForSrcExecute(amountSrcIn, tokenIn, amountSrcOutMin, tokenOut, to, deadline);
    }
    
    private bool GetAuthFlag(string method)
    {
        var authResponse = Call(Market, 0, method);
    
        Assert(authResponse.Success, "OPDEX: INVALID_FLAG_RESPONSE");

        return (bool)authResponse.ReturnValue;
    }

    private void EnsureAuthorizationFor(Address sender, Address recipient, Permissions permission)
    {
        // Skip auth if sender is recipient, the liquidity pool with authorize the recipient
        if (sender == recipient) return;
        
        var isAuthorizedResponse = Call(Market, 0, nameof(IOpdexStandardMarket.IsAuthorized), new object[] {Message.Sender, (byte)permission});
        
        Assert(isAuthorizedResponse.Success && (bool)isAuthorizedResponse.ReturnValue, "OPDEX: UNAUTHORIZED");
    }
}
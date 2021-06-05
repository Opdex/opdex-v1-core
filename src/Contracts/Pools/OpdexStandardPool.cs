using Stratis.SmartContracts;

/// <summary>
/// Standard liquidity pool including CRS and an SRC20 token along with a Liquidity Pool token (SRC20) in this contract.
/// Configurable authorizations, transaction fees and market fees are set during contract creation.
/// Mint, Swap and Burn methods should be called through an integrated Router contract.
/// </summary>
public class OpdexStandardPool : OpdexLiquidityPool, IOpdexStandardPool
{
    /// <summary>
    /// Constructor initializing a standard pool contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="token">The address of the SRC token in the pool.</param>
    /// <param name="transactionFee">The market transaction fee, 0-10 equal to 0-1%.</param>
    /// <param name="authProviders">Flag to authorize liquidity providers or not.</param>
    /// <param name="authTraders">Flag to authorize traders or not.</param>
    /// <param name="marketFeeEnabled">Flag determining if 1/6 of transaction fees are collected by the market owner.</param>
    public OpdexStandardPool(ISmartContractState state, Address token, uint transactionFee, bool authProviders, bool authTraders, bool marketFeeEnabled) 
        : base(state, token, transactionFee)
    {
        Market = Message.Sender;
        AuthProviders = authProviders;
        AuthTraders = authTraders;
        MarketFeeEnabled = marketFeeEnabled;
    }

    /// <inheritdoc />
    public override void Receive() { }
    
    /// <inheritdoc />
    public Address Market
    {
        get => State.GetAddress(nameof(Market));
        private set => State.SetAddress(nameof(Market), value);
    }
    
    /// <inheritdoc />
    public bool AuthProviders
    {
        get => State.GetBool(nameof(AuthProviders));
        private set => State.SetBool(nameof(AuthProviders), value);
    }
    
    /// <inheritdoc />
    public bool AuthTraders
    {
        get => State.GetBool(nameof(AuthTraders));
        private set => State.SetBool(nameof(AuthTraders), value);
    }
    
    /// <inheritdoc />
    public bool MarketFeeEnabled
    {
        get => State.GetBool(nameof(MarketFeeEnabled));
        private set => State.SetBool(nameof(MarketFeeEnabled), value);
    }

    /// <inheritdoc />
    public override UInt256 Mint(Address to)
    {
        EnsureUnlocked();
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Provide);

        var marketFeeEnabled = MarketFeeEnabled;
        
        if (marketFeeEnabled) MintMarketFee();
        
        var liquidity = MintExecute(to);
        
        if (marketFeeEnabled) UpdateKLast();
        
        Unlock();
        
        return liquidity;
    }
        
    /// <inheritdoc />
    public override UInt256[] Burn(Address to)
    {
        EnsureUnlocked();
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Provide);
        
        var marketFeeEnabled = MarketFeeEnabled;
        
        if (marketFeeEnabled) MintMarketFee();
        
        var amounts = BurnExecute(to, GetBalance(Address));
        
        if (marketFeeEnabled) UpdateKLast();
        
        Unlock();
        
        return amounts;
    }
    
    /// <inheritdoc />
    public override void Swap(ulong amountCrsOut, UInt256 amountSrcOut, Address to, byte[] data)
    {
        EnsureUnlocked();
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Trade);
        
        SwapExecute(amountCrsOut, amountSrcOut, to, data);
        
        Unlock();
    }
        
    /// <inheritdoc />
    public override void Skim(Address to)
    {
        EnsureUnlocked();
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Provide);
        
        SkimExecute(to);
        
        Unlock();
    }
    
    /// <inheritdoc />
    public override void Sync()
    {
        EnsureUnlocked();
        EnsureAuthorizationFor(Message.Sender, Address.Zero, Permissions.Provide);
        
        UpdateReserves(Balance, GetSrcBalance(Token, Address));
        
        Unlock();
    }
    
    private void EnsureAuthorizationFor(Address primary, Address secondary, Permissions permission)
    {
        Assert(IsAuthorized(primary, secondary, (byte)permission), "OPDEX: UNAUTHORIZED");
    }
    
    private bool IsAuthorized(Address primary, Address secondary, byte permission)
    {
        switch ((Permissions)permission)
        {
            case Permissions.Provide when !AuthProviders:
            case Permissions.Trade when !AuthTraders: return true;
            default:
                var authParameters = secondary == Address.Zero 
                    ? new object[] {primary, permission} 
                    : new object[] {primary, secondary, permission};
                
                var isAuthorizedResponse = Call(Market, 0, nameof(IOpdexStandardMarket.IsAuthorized), authParameters);

                return isAuthorizedResponse.Success && (bool)isAuthorizedResponse.ReturnValue;
        }
    }
    
    private void MintMarketFee()
    {
        var liquidity = CalculateFee();

        if (liquidity == 0) return;
        
        MintTokensExecute(Market, liquidity);
    }
}

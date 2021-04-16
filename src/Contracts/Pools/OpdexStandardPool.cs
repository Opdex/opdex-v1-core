using Stratis.SmartContracts;

/// <summary>
/// Standard liquidity pool including CRS and one SRC20 token. Methods in this contract should not be called directly
/// unless integrated through a third party contract. The controller contract has safeguards and prerequisite
/// transactions in place. Responsible for managing the pools reserves and the pool's liquidity token.
/// </summary>
public class OpdexStandardPool : OpdexPool, IOpdexStandardPool
{
    /// <summary>
    /// Constructor initializing a standard pool contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="token">The address of the SRC token in the pool.</param>
    /// <param name="authProviders"></param>
    /// <param name="authTraders"></param>
    /// <param name="fee"></param>
    public OpdexStandardPool(ISmartContractState state, Address token, bool authProviders, bool authTraders, uint fee) 
        : base(state, token, fee)
    {
        Market = Message.Sender;
        AuthProviders = authProviders;
        AuthTraders = authTraders;
    }

    /// <inheritdoc cref="IOpdexStandardPool.Receive" />
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
    public bool IsAuthorizedFor(Address address, byte permission)
    {
        switch ((Permissions)permission)
        {
            case Permissions.Provide when !AuthProviders:
            case Permissions.Trade when !AuthTraders:
                return true;
            default:
            {
                if (address == Market) return true;
                
                return (bool)Call(Market, 0, nameof(IOpdexStandardMarket.IsAuthorizedFor), new object[] {address, permission}).ReturnValue;
            }
        }
    }
        
    /// <inheritdoc />
    public override UInt256 Mint(Address to)
    {
        EnsureUnlocked();
        EnsureAuthorizationFor(Message.Sender, Permissions.Provide);

        var liquidity = MintExecute(to);
        
        Unlock();
    
        return liquidity;
    }
        
    /// <inheritdoc />
    public override UInt256[] Burn(Address to)
    {
        EnsureUnlocked();
        EnsureAuthorizationFor(Message.Sender, Permissions.Provide);
        
        var amounts = BurnExecute(to,  GetBalance(Address));
    
        Unlock();

        return amounts;
    }
        
    /// <inheritdoc />
    public override void Skim(Address to)
    {
        EnsureUnlocked();
        EnsureAuthorizationFor(Message.Sender, Permissions.Provide);
    
        SkimExecute(to);
    
        Unlock();
    }
    
    /// <inheritdoc />
    public override void Sync()
    {
        EnsureUnlocked();
        EnsureAuthorizationFor(Message.Sender, Permissions.Provide);
    
        UpdateReserves(Balance, GetSrcBalance(Token, Address));
    
        Unlock();
    }
        
    /// <inheritdoc />
    public override void Swap(ulong amountCrsOut, UInt256 amountSrcOut, Address to, byte[] data)
    {
        EnsureUnlocked();
        EnsureAuthorizationFor(Message.Sender, Permissions.Trade);
    
        SwapExecute(amountCrsOut, amountSrcOut, to, data);
    
        Unlock();
    }
    
    private void EnsureAuthorizationFor(Address address, Permissions permission)
    {
        Assert(IsAuthorizedFor(address, (byte)permission), "OPDEX: UNAUTHORIZED");
    }
}

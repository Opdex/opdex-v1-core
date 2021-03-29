using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

/// <summary>
/// Standard SRC 256 token used for measuring users liquidity position when providing to liquidity pools.
/// </summary>
public abstract class OpdexLiquidityPoolToken : SmartContract, IStandardToken256
{
    private const string TokenSymbol = "OLPT";
    private const string TokenName = "Opdex Liquidity Pool Token";
    private const byte TokenDecimals = 8;
    
    /// <summary>
    /// Constructor initializing the liquidity pool token contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    protected OpdexLiquidityPoolToken(ISmartContractState state) : base(state)
    {
    }
    
    /// <inheritdoc />
    public byte Decimals => TokenDecimals;
    
    public string Name => TokenName;
    
    public string Symbol => TokenSymbol;
    
    /// <inheritdoc />
    public UInt256 TotalSupply 
    {
        get => State.GetUInt256(nameof(TotalSupply));
        private set => State.SetUInt256(nameof(TotalSupply), value);
    }

    /// <inheritdoc />
    public UInt256 GetBalance(Address address)
    {
        return State.GetUInt256($"Balance:{address}");
    } 

    private void SetBalance(Address address, UInt256 amount)
    {
        State.SetUInt256($"Balance:{address}", amount);
    }

    /// <inheritdoc />
    public UInt256 Allowance(Address owner, Address spender)
    {
        return State.GetUInt256($"Allowance:{owner}:{spender}");
    }

    private void SetAllowance(Address owner, Address spender, UInt256 amount)
    {
        State.SetUInt256($"Allowance:{owner}:{spender}", amount);
    }

    /// <inheritdoc />
    public bool TransferTo(Address to, UInt256 amount)
    {
        return TransferTokensExecute(Message.Sender, to, amount);
    }
    
    /// <inheritdoc />
    public bool Approve(Address spender, UInt256 currentAmount, UInt256 amount)
    {
        if (Allowance(Message.Sender, spender) != currentAmount) return false;

        SetAllowance(Message.Sender, spender, amount);
        
        Log(new OpdexApprovalEvent { Owner = Message.Sender, Spender = spender, Amount = amount});
        
        return true;
    }
    
    /// <inheritdoc />
    public bool TransferFrom(Address from, Address to, UInt256 amount)
    {
        var allowance = Allowance(from, Message.Sender);
        
        if (allowance > 0) SetAllowance(from, Message.Sender, allowance - amount);
        
        return TransferTokensExecute(from, to, amount);
    }
    
    protected bool TransferTokensExecute(Address from, Address to, UInt256 amount)
    {
        SetBalance(from, GetBalance(from) - amount);
        SetBalance(to, GetBalance(to) + amount);
        
        Log(new OpdexTransferEvent { From = from,  To = to,  Amount = amount });
        
        return true;
    }
    
    protected void MintTokensExecute(Address to, UInt256 amount)
    {
        TotalSupply += amount;

        SetBalance(to, GetBalance(to) + amount);

        Log(new OpdexTransferEvent { From = Address.Zero,  To = to,  Amount = amount });
    }
    
    protected void BurnTokensExecute(Address from, UInt256 amount)
    {
        TotalSupply -= amount;

        SetBalance(from, GetBalance(from) - amount);
        
        Log(new OpdexTransferEvent{ From = from, To = Address.Zero,  Amount = amount });
    }
}
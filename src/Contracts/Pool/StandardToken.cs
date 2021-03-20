using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

public abstract class StandardToken : ContractBase, IStandardToken256
{
    private const string TokenSymbol = "OLPT";
    private const string TokenName = "Opdex Liquidity Pool Token";
    private const byte TokenDecimals = 8;
    
    protected StandardToken(ISmartContractState contractState) : base(contractState)
    {
    }
    
    public byte Decimals => TokenDecimals;
    
    public string Name => TokenName;
    
    public string Symbol => TokenSymbol;
    
    public UInt256 TotalSupply 
    {
        get => State.GetUInt256(nameof(TotalSupply));
        private set => State.SetUInt256(nameof(TotalSupply), value);
    }
    
    public UInt256 GetBalance(Address address) 
        => State.GetUInt256($"Balance:{address}");

    private void SetBalance(Address address, UInt256 amount) => 
        State.SetUInt256($"Balance:{address}", amount);

    // IStandardToken256 interface compatibility
    public UInt256 Allowance(Address owner, Address spender) 
        => GetAllowance(owner, spender);

    public UInt256 GetAllowance(Address owner, Address spender)
        => State.GetUInt256($"Allowance:{owner}:{spender}");

    private void SetAllowance(Address owner, Address spender, UInt256 amount)
        => State.SetUInt256($"Allowance:{owner}:{spender}", amount);

    public bool TransferTo(Address to, UInt256 amount)
        => TransferTokensExecute(Message.Sender, to, amount);
    
    public bool TransferFrom(Address from, Address to, UInt256 amount)
    {
        var allowance = GetAllowance(from, Message.Sender);
        
        if (allowance > 0) SetAllowance(from, Message.Sender, allowance - amount);
        
        return TransferTokensExecute(from, to, amount);
    }

    // IStandardToken256 interface compatibility
    public bool Approve(Address spender, UInt256 currentAmount, UInt256 amount) 
        => Approve(spender, amount);
    
    public bool Approve(Address spender, UInt256 amount)
    {
        SetAllowance(Message.Sender, spender, amount);
        
        Log(new OpdexApprovalEvent
        {
            Owner = Message.Sender, 
            Spender = spender, 
            Amount = amount
        });
        
        return true;
    }
    
    protected bool TransferTokensExecute(Address from, Address to, UInt256 amount)
    {
        SetBalance(from, GetBalance(from) - amount);
        SetBalance(to, GetBalance(to) + amount);
        
        LogTransferEvent(from, to, amount);
        
        return true;
    }
    
    protected void MintTokensExecute(Address to, UInt256 amount)
    {
        TotalSupply += amount;

        SetBalance(to, GetBalance(to) + amount);

        LogTransferEvent(Address.Zero, to, amount);
    }
    
    protected void BurnTokensExecute(Address from, UInt256 amount)
    {
        TotalSupply -= amount;

        SetBalance(from, GetBalance(from) - amount);
        
        LogTransferEvent(from, Address.Zero, amount);
    }
    
    private void LogTransferEvent(Address from, Address to, UInt256 amount)
    {
        Log(new OpdexTransferEvent
        {
            From = from, 
            To = to, 
            Amount = amount
        });
    }
}
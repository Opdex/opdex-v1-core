using Stratis.SmartContracts;

public abstract class OpdexV1SRC : SmartContract
{
    public const string Name = "OpdexV1";
    public const string Symbol = "OPDV1";
    public const short Decimals = 8;

    protected OpdexV1SRC(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public ulong GetBalance(Address address) => PersistentState.GetUInt64($"Balance:{address}");

    private void SetBalance(Address address, ulong value) => PersistentState.SetUInt64($"Balance:{address}", value);

    public ulong GetAllowance(Address owner, Address spender) => PersistentState.GetUInt64($"Allowance:{owner}:{spender}");
    
    private void SetAllowance(Address owner, Address spender, ulong value) => PersistentState.SetUInt64($"Allowance:{owner}:{spender}", value);

    public ulong TotalSupply 
    {
        get => PersistentState.GetUInt64(nameof(TotalSupply));
        private set => PersistentState.SetUInt64(nameof(TotalSupply), value);
    }
    
    public bool Approve(Address spender, ulong value) => ApproveExecute(Message.Sender, spender, value);

    public bool TransferTo(Address to, ulong value) => TransferExecute(Message.Sender, to, value);

    public bool TransferFrom(Address from, Address to, ulong value)
    {
        var allowance = GetAllowance(from, Message.Sender);
        if (allowance > 0) SetAllowance(from, Message.Sender, SafeMath.Sub(allowance, value));
        return TransferExecute(from, to, value);
    }
    
    protected void MintExecute(Address to, ulong value)
    {
        var balance = GetBalance(to);
        TotalSupply = SafeMath.Add(TotalSupply, value);
        SetBalance(to, SafeMath.Add(balance, value));
        Log(new TransferEvent { From = Address.Zero, To = to, Value = value });
    }

    protected void BurnExecute(Address from, ulong value)
    {
        var balance = GetBalance(from);
        SetBalance(from, SafeMath.Sub(balance, value));
        TotalSupply = SafeMath.Sub(TotalSupply, value);
        Log(new TransferEvent { From = from, To = Address.Zero, Value = value });
    }

    private bool ApproveExecute(Address owner, Address spender, ulong value)
    {
        SetAllowance(owner, spender, value);
        Log(new ApprovalEvent { Owner = owner, Spender = spender, Value = value });
        return true;
    }

    private bool TransferExecute(Address from, Address to, ulong value)
    {
        var fromBalance = GetBalance(from);
        SetBalance(from, SafeMath.Sub(fromBalance, value));
        var toBalance = GetBalance(to);
        SetBalance(to, SafeMath.Add(toBalance, value));
        Log(new TransferEvent {From = from, To = to, Value = value});
        return true;
    }

    public struct ApprovalEvent
    {
        [Index] public Address Owner;
        [Index] public Address Spender;
        public ulong Value;
    }

    public struct TransferEvent
    {
        [Index] public Address From;
        [Index] public Address To;
        public ulong Value;
    }
}
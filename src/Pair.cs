using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

public class OpdexV1Pair : ContractBase, IStandardToken256
{
    private const ulong MinimumLiquidity = 1000;
    private const string TokenSymbol = "OLPT";
    private const string TokenName = "Opdex Liquidity Pool Token";
    private const byte TokenDecimals = 8;
    
    public OpdexV1Pair(ISmartContractState smartContractState, Address token) : base(smartContractState)
    {
        Controller = Message.Sender;
        Token = token;
    }
    
    public byte Decimals => TokenDecimals;
    
    public string Name => TokenName;
    
    public string Symbol => TokenSymbol;

    public Address Controller
    {
        get => State.GetAddress(nameof(Controller));
        private set => State.SetAddress(nameof(Controller), value);
    }
    
    public Address Token
    {
        get => State.GetAddress(nameof(Token));
        private set => State.SetAddress(nameof(Token), value);
    }
    
    public ulong ReserveCrs
    {
        get => State.GetUInt64(nameof(ReserveCrs));
        private set => State.SetUInt64(nameof(ReserveCrs), value);
    }
    
    public UInt256 ReserveToken
    {
        get => State.GetUInt256(nameof(ReserveToken));
        private set => State.SetUInt256(nameof(ReserveToken), value);
    }
    
    public UInt256 KLast
    {
        get => State.GetUInt256(nameof(KLast));
        private set => State.SetUInt256(nameof(KLast), value);
    }

    public UInt256 TotalSupply 
    {
        get => State.GetUInt256(nameof(TotalSupply));
        private set => State.SetUInt256(nameof(TotalSupply), value);
    }

    public UInt256 GetBalance(Address address)
    {
        return State.GetUInt256($"Balance:{address}");
    }

    private void SetBalance(Address address, UInt256 amount)
    {
        State.SetUInt256($"Balance:{address}", amount);
    }

    // Added for IStandardToken interface compatibility
    public UInt256 Allowance(Address owner, Address spender)
    {
        return GetAllowance(owner, spender);
    }
    
    public UInt256 GetAllowance(Address owner, Address spender)
    {
        return State.GetUInt256($"Allowance:{owner}:{spender}");
    }

    private void SetAllowance(Address owner, Address spender, UInt256 amount)
    {
        State.SetUInt256($"Allowance:{owner}:{spender}", amount);
    }

    public bool TransferTo(Address to, UInt256 amount)
    {
        return TransferExecute(Message.Sender, to, amount);
    }
    
    public bool TransferFrom(Address from, Address to, UInt256 amount)
    {
        var allowance = GetAllowance(from, Message.Sender);
        if (allowance > 0) SetAllowance(from, Message.Sender, allowance - amount);
        return TransferExecute(from, to, amount);
    }

    // Added for IStandardToken interface compatibility, currentAmount goes unused
    public bool Approve(Address spender, UInt256 currentAmount, UInt256 amount)
    {
        return Approve(spender, amount);
    }
    
    public bool Approve(Address spender, UInt256 amount)
    {
        SetAllowance(Message.Sender, spender, amount);
        Log(new ApprovalEvent {Owner = Message.Sender, Spender = spender, Amount = amount, EventTypeId = (byte)EventType.ApprovalEvent});
        return true;
    }

    public UInt256 Mint(Address to)
    {
        var reserves = GetReserves();
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(Token, Address);
        var amountCrs = (ulong)(balanceCrs - reserves[0]);
        var amountToken = balanceToken - reserves[1];
        var totalSupply = TotalSupply;
        MintFee((ulong)reserves[0], reserves[1]);
        UInt256 liquidity;
        if (totalSupply == 0)
        {
            liquidity = Sqrt(amountCrs * amountToken) - MinimumLiquidity;
            MintExecute(Address.Zero, MinimumLiquidity);
        }
        else
        {
            var amountCrsLiquidity = (amountCrs * totalSupply) / reserves[0];
            var amountTokenLiquidity = (amountToken * totalSupply) / reserves[1];
            liquidity = amountCrsLiquidity > amountTokenLiquidity ? amountTokenLiquidity : amountCrsLiquidity;
        }
        Assert(liquidity > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        MintExecute(to, liquidity);
        Update(balanceCrs, balanceToken);
        KLast = ReserveCrs * ReserveToken;
        Log(new MintEvent { AmountCrs = amountCrs, AmountToken = amountToken, Sender = Message.Sender, EventTypeId = (byte)EventType.MintEvent });
        return liquidity;
    }

    public UInt256[] Burn(Address to)
    {
        var reserves = GetReserves();
        var address = Address;
        var token = Token;
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(token, address);
        var liquidity = GetBalance(address);
        var totalSupply = TotalSupply;
        var amountCrs = (ulong)((liquidity * balanceCrs) / totalSupply);
        var amountToken = (liquidity * balanceToken) / totalSupply;
        Assert(amountCrs > 0 && amountToken > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY_BURNED");
        MintFee((ulong)reserves[0], reserves[1]);
        BurnExecute(address, liquidity);
        SafeTransfer(to, amountCrs);
        SafeTransferTo(token, to, amountToken);
        balanceCrs = Balance;
        balanceToken = GetSrcBalance(token, address);
        Update(balanceCrs, balanceToken);
        Log(new BurnEvent { Sender = Message.Sender, To = to, AmountCrs = amountCrs, AmountToken = amountToken, EventTypeId = (byte)EventType.BurnEvent });
        return new [] {amountCrs, amountToken};
    }

    public void Swap(ulong amountCrsOut, UInt256 amountTokenOut, Address to)
    {
        var reserves = GetReserves();
        var token = Token;
        Assert(amountCrsOut > 0 ^ amountTokenOut > 0, "OpdexV1: INVALID_OUTPUT_AMOUNT");
        Assert(amountCrsOut < reserves[0] && amountTokenOut < reserves[1], "OpdexV1: INSUFFICIENT_LIQUIDITY");
        Assert(to != token, "OpdexV1: INVALID_TO");
        SafeTransfer(to, amountCrsOut);
        SafeTransferTo(token, to, amountTokenOut);
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(token, Address);
        var crsDifference = (reserves[0] - amountCrsOut);
        var amountCrsIn = balanceCrs > crsDifference ? (ulong)(balanceCrs - crsDifference) : 0;
        var srcDifference = (reserves[1] - amountTokenOut);
        var amountTokenIn = balanceToken > srcDifference ? balanceToken - srcDifference : 0;
        Assert(amountCrsIn > 0 || amountTokenIn > 0, "OpdexV1: INSUFFICIENT_INPUT_AMOUNT");
        var balanceCrsAdjusted = (balanceCrs * 1_000) - (amountCrsIn * 3);
        var balanceTokenAdjusted = (balanceToken * 1_000) - (amountTokenIn * 3);
        Assert(balanceCrsAdjusted * balanceTokenAdjusted >= (reserves[0] * reserves[1]) * 1_000_000); // 1_000 * 1_000
        Update(balanceCrs, balanceToken);
        KLast = ReserveCrs * ReserveToken;
        Log(new SwapEvent { AmountCrsIn = amountCrsIn, AmountCrsOut = amountCrsOut, AmountTokenIn = amountTokenIn,
             AmountTokenOut = amountTokenOut, Sender = Message.Sender, To = to, EventTypeId = (byte)EventType.SwapEvent });
    }

    public void Borrow(ulong amountCrs, UInt256 amountToken, Address to, string callbackMethod, byte[] data)
    {
        var token = Token;
        Assert(to != Address.Zero && to != Address && to != token);
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(token, Address);
        SafeTransferTo(token, to, amountToken);
        var result = Call(to, amountCrs, callbackMethod, new object[] {data});
        Assert(result.Success);
        Assert(balanceCrs == Balance && balanceToken == GetSrcBalance(token, Address), "OpdexV1: INSUFFICIENT_DEBT_PAID");
    }

    // public void Stake(Address to)
    // {
    //     // Check last saved staking weight of contract
    //     // Use difference of now and last saved to determine this users weight
    //     // KLast calcs
    //     // Persist struct, users weight, klast
    // }
    //
    // public void StopStaking(Address to)
    // {
    //     // Stop staking, return OPDT 
    //     WithdrawStakingRewards(to);
    // }
    //
    // public void WithdrawStakingRewards(Address to)
    // {
    //     // Keep staking, withdraw rewards
    //     // Uses stakers weight along with LP tokens assigned to this pairs Address
    // }

    public void Skim(Address to)
    {
        var token = Token;
        var balanceToken = GetSrcBalance(token, Address) - ReserveToken;
        var balanceCrs = Balance - ReserveCrs;
        SafeTransfer(to, balanceCrs);
        SafeTransferTo(token, to, balanceToken);
    }

    public void Sync()
    {
        Update(Balance, GetSrcBalance(Token, Address));
    }

    public UInt256[] GetReserves()
    {
        return new [] { ReserveCrs, ReserveToken };
    }
    
    private void Update(ulong balanceCrs, UInt256 balanceToken)
    {
        ReserveCrs = balanceCrs;
        ReserveToken = balanceToken;
        Log(new SyncEvent { ReserveCrs = balanceCrs, ReserveToken = balanceToken, EventTypeId = (byte)EventType.SyncEvent });
    }
    
    private void MintFee(ulong reserveCrs, UInt256 reserveToken)
    {
        var kLast = KLast;
        if (kLast == 0) return;
        var rootK = Sqrt(reserveCrs * reserveToken);
        var rootKLast = Sqrt(kLast);
        if (rootK <= rootKLast) return;
        var numerator = TotalSupply * (rootK - rootKLast);
        var denominator = (rootK * 5) + rootKLast;
        var liquidity = numerator / denominator;
        if (liquidity == 0) return;
        var feeToResponse = Call(Controller, 0, "get_FeeTo");
        var feeTo = (Address)feeToResponse.ReturnValue;
        Assert(feeToResponse.Success && feeTo != Address.Zero, "OpdexV1: INVALID_FEE_TO_ADDRESS");
        MintExecute(feeTo, liquidity); // Staking would mint the fee to this pairs Address
    }
    
    private void MintExecute(Address to, UInt256 amount)
    {
        TotalSupply += amount;
        SetBalance(to, GetBalance(to) + amount);
        Log(new TransferEvent { From = Address.Zero, To = to, Amount = amount, EventTypeId = (byte)EventType.TransferEvent });
    }
    
    private UInt256 GetSrcBalance(Address token, Address owner)
    {
        var balanceResponse = Call(token, 0, "GetBalance", new object[] {owner});
        Assert(balanceResponse.Success, "OpdexV1: INVALID_BALANCE");
        return (UInt256)balanceResponse.ReturnValue;
    }
    
    private void BurnExecute(Address from, UInt256 amount)
    {
        SetBalance(from, GetBalance(from) - amount);
        TotalSupply -= amount;
        Log(new TransferEvent { From = from, To = Address.Zero, Amount = amount, EventTypeId = (byte)EventType.TransferEvent });
    }
    
    private bool TransferExecute(Address from, Address to, UInt256 amount)
    {
        SetBalance(from, GetBalance(from) - amount);
        SetBalance(to, GetBalance(to) + amount);
        Log(new TransferEvent {From = from, To = to, Amount = amount, EventTypeId = (byte)EventType.TransferEvent});
        return true;
    }
    
    private static UInt256 Sqrt(UInt256 value)
    {
        if (value <= 3) return 1;
        var result = value;
        var root = value / 2 + 1;
        while (root < result) 
        {
            result = root;
            root = (value / root + root) / 2;
        }
        return result;
    }
}
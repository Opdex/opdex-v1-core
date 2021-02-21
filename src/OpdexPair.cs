using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

public class OpdexPair : ContractBase, IStandardToken256
{
    private const ulong MinimumLiquidity = 1000;
    private const string TokenSymbol = "OLPT";
    private const string TokenName = "Opdex Liquidity Pool Token";
    private const byte TokenDecimals = 8;
    
    public OpdexPair(ISmartContractState smartContractState, Address token, Address stakeToken) : base(smartContractState)
    {
        Controller = Message.Sender;
        Token = token;
        StakeToken = stakeToken;
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

    public Address StakeToken
    {
        get => State.GetAddress(nameof(StakeToken));
        private set => State.SetAddress(nameof(StakeToken), value);
    }
    
    public ulong ReserveCrs
    {
        get => State.GetUInt64(nameof(ReserveCrs));
        private set => State.SetUInt64(nameof(ReserveCrs), value);
    }
    
    public UInt256 ReserveSrc
    {
        get => State.GetUInt256(nameof(ReserveSrc));
        private set => State.SetUInt256(nameof(ReserveSrc), value);
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

    public UInt256 TotalWeight
    {
        get => State.GetUInt256(nameof(TotalWeight));
        private set => State.SetUInt256(nameof(TotalWeight), value);
    }

    // public UInt256 GetWeight(Address address)
    // {
    //     return State.GetUInt256($"Weight:{address}");
    // }
    //
    // public void SetWeight(Address address, UInt256 weight)
    // {
    //     State.SetUInt256($"Weight:{address}", weight);
    // }
    //
    // public UInt256 GetWeightK(Address address)
    // {
    //     return State.GetUInt256($"WeightK:{address}");
    // }
    //
    // public void SetWeightK(Address address, UInt256 weightK)
    // {
    //     State.SetUInt256($"WeightK:{address}", weightK);
    // }

    public bool Locked
    {
        get => State.GetBool(nameof(Locked));
        private set => State.SetBool(nameof(Locked), value);
    }

    public UInt256 GetBalance(Address address)
    {
        return State.GetUInt256($"Balance:{address}");
    }

    private void SetBalance(Address address, UInt256 amount)
    {
        State.SetUInt256($"Balance:{address}", amount);
    }

    // IStandardToken256 interface compatibility
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

    // IStandardToken256 interface compatibility
    public bool Approve(Address spender, UInt256 currentAmount, UInt256 amount)
    {
        return Approve(spender, amount);
    }
    
    public bool Approve(Address spender, UInt256 amount)
    {
        SetAllowance(Message.Sender, spender, amount);

        LogApprovalEvent(Message.Sender, spender, amount, EventType.ApprovalEvent);
        
        return true;
    }

    public UInt256 Mint(Address to)
    {
        EnsureUnlocked();
        
        var reserveCrs = ReserveCrs;
        var reserveSrc = ReserveSrc;
        var balanceCrs = Balance;
        var balanceSrc = GetSrcBalance(Token, Address);
        var amountCrs = balanceCrs - reserveCrs;
        var amountSrc = balanceSrc - reserveSrc;
        var totalSupply = TotalSupply;
        
        MintFee(reserveCrs, reserveSrc);
        
        UInt256 liquidity;
        if (totalSupply == 0)
        {
            liquidity = Sqrt(amountCrs * amountSrc) - MinimumLiquidity;
            MintExecute(Address.Zero, MinimumLiquidity);
        }
        else
        {
            var amountCrsLiquidity = amountCrs * totalSupply / reserveCrs;
            var amountSrcLiquidity = amountSrc * totalSupply / reserveSrc;
            liquidity = amountCrsLiquidity > amountSrcLiquidity ? amountSrcLiquidity : amountCrsLiquidity;
        }
        
        Assert(liquidity > 0, "OPDEX: INSUFFICIENT_LIQUIDITY");
        
        MintExecute(to, liquidity);
        Update(balanceCrs, balanceSrc);
        
        KLast = ReserveCrs * ReserveSrc;

        LogMintEvent(amountCrs, amountSrc, Message.Sender, EventType.MintEvent);
            
        return liquidity;
    }

    public UInt256[] Burn(Address to)
    {
        EnsureUnlocked();
        
        var reserveCrs = ReserveCrs;
        var reserveSrc = ReserveSrc;
        var address = Address;
        var token = Token;
        var balanceCrs = Balance;
        var balanceSrc = GetSrcBalance(token, address);
        // If staking rewards are assigned to this contracts address, subtract it from the liquidity to burn
        var liquidity = GetBalance(address);
        var totalSupply = TotalSupply;
        var amountCrs = (ulong)(liquidity * balanceCrs / totalSupply);
        var amountSrc = liquidity * balanceSrc / totalSupply;
        
        Assert(amountCrs > 0 && amountSrc > 0, "OPDEX: INSUFFICIENT_LIQUIDITY_BURNED");
        
        MintFee(reserveCrs, reserveSrc);
        BurnExecute(address, liquidity);
        SafeTransfer(to, amountCrs);
        SafeTransferTo(token, to, amountSrc);
        
        balanceCrs = Balance;
        balanceSrc = GetSrcBalance(token, address);
        
        Update(balanceCrs, balanceSrc);
        
        KLast = ReserveCrs * ReserveSrc;

        LogBurnEvent(amountCrs, amountSrc, Message.Sender, to, EventType.BurnEvent);
        
        return new [] {amountCrs, amountSrc};
    }

    public void Swap(ulong amountCrsOut, UInt256 amountSrcOut, Address to, byte[] data)
    {
        EnsureUnlocked();
        
        var reserveCrs = ReserveCrs;
        var reserveSrc = ReserveSrc;
        var token = Token;
        
        Assert(amountCrsOut > 0 ^ amountSrcOut > 0, "OPDEX: INVALID_OUTPUT_AMOUNT");
        Assert(amountCrsOut < reserveCrs && amountSrcOut < reserveSrc, "OPDEX: INSUFFICIENT_LIQUIDITY");
        Assert(to != token && to != Address, "OPDEX: INVALID_TO");
        
        SafeTransfer(to, amountCrsOut);
        SafeTransferTo(token, to, amountSrcOut);

        if (data.Length > 0)
        {
            var callbackData = Serializer.ToStruct<CallbackData>(data);
            var parameters = callbackData.Data.Length > 0 ? new object[] {callbackData.Data} : null;
            Call(to, 0, callbackData.Method, parameters);
        }
        
        var balanceCrs = Balance;
        var balanceSrc = GetSrcBalance(token, Address);
        var crsDifference = reserveCrs - amountCrsOut;
        var amountCrsIn = balanceCrs > crsDifference ? balanceCrs - crsDifference : 0;
        var srcDifference = reserveSrc - amountSrcOut;
        var amountSrcIn = balanceSrc > srcDifference ? balanceSrc - srcDifference : 0;
        
        Assert(amountCrsIn > 0 || amountSrcIn > 0, "OPDEX: ZERO_INPUT_AMOUNT");
        
        var balanceCrsAdjusted = (balanceCrs * 1_000) - (amountCrsIn * 3);
        var balanceSrcAdjusted = (balanceSrc * 1_000) - (amountSrcIn * 3);
        
        Assert(balanceCrsAdjusted * balanceSrcAdjusted >= reserveCrs * reserveSrc * 1_000_000, "OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        
        Update(balanceCrs, balanceSrc);
        
        LogSwapEvent(amountCrsIn, amountCrsOut, amountSrcIn, amountSrcOut, Message.Sender, to, EventType.SwapEvent);
    }

    // - Handle Address Balance = 0 Issues
    //   - Either stakers can't stake until MintFee is called and this Address has an LP balance
    //   - Or, part of the initial burned fee of add liquidity is sent to here to allow staking immediately
    //   - - test scenarios and calculations with initializing immediately with burned fee
    // public void Stake(Address to, UInt256 weight)
    // {
    //     // - How to handle adding on with extra weight
    //     // TransferFrom only if this is to be called directly, else TransferTo - probably should be TransferTo going through controller
    //     // Would mean we check the difference between balance and totalWeight
    //     SafeTransferFrom(StakeToken, Message.Sender, Address, weight);
    //     SetWeight(to, weight);
    //     var weightK = weight * GetBalance(Address) / TotalWeight; // - This will return a floating point number, adjust for sats
    //     SetWeightK(Address, weightK);
    //     // Verify this 99% sure TotalWeight gets updated **after** finding weightK
    //     TotalWeight += weight;
    // }
    
    // - Add shared methods, this does some things twice in combination with WithdrawStakingRewards
    // - Asserts and validations
    // - Coming in from Controller
    // public void StopStaking(Address to)
    // {
    //     var weight = GetWeight(Message.Sender);
    //     WithdrawStakingRewards(to);
    //     SafeTransferTo(StakeToken, to, weight);
    //     SetWeight(Message.Sender, 0);
    //     SetWeightK(Message.Sender, 0);
    //     TotalWeight -= weight;
    // }
    
    // - Add another method, one for withdrawing LP tokens, one for total withdraw from reserves
    // - In order to not use Message.Sender, we have to expect something sent in the same transaction
    // - similar to how liquidity pool tokens are expected to be sent back first, in order to burn.
    // public void WithdrawStakingRewards(Address to)
    // {
    //     // Keep staking, withdraw rewards
    //     var totalWeight = TotalWeight;
    //     var weight = GetWeight(Message.Sender);
    //     var weightKLast = GetWeightK(Message.Sender);
    //     var weightK = weight * GetBalance(Address) / totalWeight;
    //     Assert(weightK > weightKLast); // maybe just return;
    //     var rewards = weightK - weightKLast;
    //     TransferExecute(Address, to, rewards);
    //     var updateWeightK = weight * GetBalance(Address) / totalWeight;
    //     SetWeight(Message.Sender, updateWeightK); // Should this recalc? The Address balance would be different
    // }
    
    // public void WithdrawStakingRewardsAndBurn(Address to)
    // {
    //     WithdrawStakingRewards(Address);
    //     Burn(to);
    // }

    public void Skim(Address to)
    {
        EnsureUnlocked();
        
        var token = Token;
        var balanceSrc = GetSrcBalance(token, Address) - ReserveSrc;
        var balanceCrs = Balance - ReserveCrs;
        
        SafeTransfer(to, balanceCrs);
        SafeTransferTo(token, to, balanceSrc);
    }

    public void Sync()
    {
        EnsureUnlocked();
        
        Update(Balance, GetSrcBalance(Token, Address));
    }

    public byte[][] GetReserves()
    {
        return new [] { Serializer.Serialize(ReserveCrs), Serializer.Serialize(ReserveSrc) };
    }
    
    private void Update(ulong balanceCrs, UInt256 balanceSrc)
    {
        ReserveCrs = balanceCrs;
        
        ReserveSrc = balanceSrc;

        LogSyncEvent(balanceCrs, balanceSrc, EventType.SyncEvent);
    }
    
    private void MintFee(ulong reserveCrs, UInt256 reserveSrc)
    {
        var kLast = KLast;
        
        if (kLast == 0) return;
        
        var rootK = Sqrt(reserveCrs * reserveSrc);
        var rootKLast = Sqrt(kLast);
        
        if (rootK <= rootKLast) return;
        
        var numerator = TotalSupply * (rootK - rootKLast);
        var denominator = (rootK * 5) + rootKLast;
        var liquidity = numerator / denominator;
        
        if (liquidity == 0) return;
        
        var feeToResponse = Call(Controller, 0, "get_FeeTo");
        var feeTo = (Address)feeToResponse.ReturnValue;
        
        Assert(feeToResponse.Success && feeTo != Address.Zero, "OPDEX: INVALID_FEE_TO_ADDRESS");
        
        // Adjust feeTo for staking
        // Staking theoretically would mint to this pairs address
        // That will be problematic for removing liquidity as users transfer LP to this contract
        // then the balance is checked to find out how much to burn etc.
        // Possibly add persistent state check for staking LP tokens and calculate
        // GetBalance(Address) - State.StakingLPT
        MintExecute(feeTo, liquidity);
    }
    
    private void MintExecute(Address to, UInt256 amount)
    {
        TotalSupply += amount;
        
        SetBalance(to, GetBalance(to) + amount);
        
        LogTransferEvent(Address.Zero, to, amount, EventType.TransferEvent);
    }
    
    private UInt256 GetSrcBalance(Address token, Address owner)
    {
        var balanceResponse = Call(token, 0, "GetBalance", new object[] {owner});
        
        Assert(balanceResponse.Success, "OPDEX: INVALID_BALANCE");
        
        return (UInt256)balanceResponse.ReturnValue;
    }
    
    private void BurnExecute(Address from, UInt256 amount)
    {
        SetBalance(from, GetBalance(from) - amount);
        
        TotalSupply -= amount;

        LogTransferEvent(from, Address.Zero, amount, EventType.TransferEvent);
    }
    
    private bool TransferExecute(Address from, Address to, UInt256 amount)
    {
        SetBalance(from, GetBalance(from) - amount);
        SetBalance(to, GetBalance(to) + amount);
        
        LogTransferEvent(from, to, amount, EventType.TransferEvent);
        
        return true;
    }
    
    private void EnsureUnlocked()
    {
        Assert(!Locked, "OPDEX: PAIR_LOCKED");
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

    private void LogApprovalEvent(Address owner, Address spender, UInt256 amount, EventType eventType)
    {
        Log(new ApprovalEvent
        {
            Owner = owner, 
            Spender = spender, 
            Amount = amount, 
            EventTypeId = (byte)eventType
        });
    }

    private void LogMintEvent(ulong amountCrs, UInt256 amountSrc, Address sender, EventType eventType)
    {
        Log(new MintEvent
        {
            AmountCrs = amountCrs, 
            AmountSrc = amountSrc, 
            Sender = sender, 
            EventTypeId = (byte)eventType
        });
    }

    private void LogBurnEvent(ulong amountCrs, UInt256 amountSrc, Address sender, Address to, EventType eventType)
    {
        Log(new BurnEvent
        {
            AmountCrs = amountCrs, 
            AmountSrc = amountSrc, 
            Sender = sender, 
            To = to,
            EventTypeId = (byte)eventType
        });
    }

    private void LogSwapEvent(ulong amountCrsIn, ulong amountCrsOut, UInt256 amountSrcIn, UInt256 amountSrcOut, 
        Address from, Address to, EventType eventType)
    {
        Log(new SwapEvent 
        { 
            AmountCrsIn = amountCrsIn, 
            AmountCrsOut = amountCrsOut, 
            AmountSrcIn = amountSrcIn,
            AmountSrcOut = amountSrcOut, 
            Sender = from, 
            To = to, 
            EventTypeId = (byte)eventType
        });
    }

    private void LogSyncEvent(ulong reserveCrs, UInt256 reserveSrc, EventType eventType)
    {
        Log(new SyncEvent
        {
            ReserveCrs = reserveCrs, 
            ReserveSrc = reserveSrc, 
            EventTypeId = (byte)eventType
        });
    }

    private void LogTransferEvent(Address from, Address to, UInt256 amount, EventType eventType)
    {
        Log(new TransferEvent
        {
            From = from, 
            To = to, 
            Amount = amount, 
            EventTypeId = (byte)eventType
        });
    }
}
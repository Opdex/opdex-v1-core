using Stratis.SmartContracts;

/// <summary>
/// Base liquidity pool including CRS and an SRC20 token along with a Liquidity Pool token (SRC20) in this contract.
/// Mint, Swap and Burn methods should be called through an integrated Router contract.
/// </summary>
public abstract class OpdexLiquidityPool : SmartContract, IOpdexLiquidityPool
{
    private const ulong MinimumLiquidity = 1000;
    private const string TokenSymbol = "OLPT";
    private const string TokenName = "Opdex Liquidity Pool Token";
    private const byte TokenDecimals = 8;

    /// <summary>
    /// Constructor initializing a standard pool contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="token">The address of the SRC token in the pool.</param>
    /// <param name="transactionFee">The market transaction fee, 0-10 equal to 0-1%.</param>
    protected OpdexLiquidityPool(ISmartContractState state, Address token, uint transactionFee) : base(state)
    {
        Assert(transactionFee <= 10, "OPDEX: INVALID_TRANSACTION_FEE");
        Token = token;
        TransactionFee = transactionFee;
    }
    
    /// <inheritdoc />
    public byte Decimals => TokenDecimals;
    
    /// <inheritdoc />
    public string Name => TokenName;
    
    /// <inheritdoc />
    public string Symbol => TokenSymbol;
    
    /// <inheritdoc />
    public UInt256 TotalSupply 
    {
        get => State.GetUInt256(nameof(TotalSupply));
        private set => State.SetUInt256(nameof(TotalSupply), value);
    }
    
    /// <inheritdoc />
    public uint TransactionFee
    {
        get => State.GetUInt32(nameof(TransactionFee));
        private set => State.SetUInt32(nameof(TransactionFee), value);
    }

    /// <inheritdoc />
    public Address Token
    {
        get => State.GetAddress(nameof(Token));
        private set => State.SetAddress(nameof(Token), value);
    }
        
    /// <inheritdoc />
    public ulong ReserveCrs
    {
        get => State.GetUInt64(nameof(ReserveCrs));
        private set => State.SetUInt64(nameof(ReserveCrs), value);
    }
    
    /// <inheritdoc />
    public UInt256 ReserveSrc
    {
        get => State.GetUInt256(nameof(ReserveSrc));
        private set => State.SetUInt256(nameof(ReserveSrc), value);
    }
        
    /// <inheritdoc />
    public UInt256 KLast
    {
        get => State.GetUInt256(nameof(KLast));
        private set => State.SetUInt256(nameof(KLast), value);
    }
        
    /// <inheritdoc />
    public bool Locked
    {
        get => State.GetBool(nameof(Locked));
        private set => State.SetBool(nameof(Locked), value);
    }
    
    /// <inheritdoc />
    public UInt256[] Reserves => new [] { ReserveCrs, ReserveSrc };
    
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
    public abstract UInt256 Mint(Address to);
    
    protected UInt256 MintExecute(Address to)
    {
        var reserveCrs = ReserveCrs;
        var reserveSrc = ReserveSrc;
        var balanceCrs = Balance;
        var balanceSrc = GetSrcBalance(Token, Address);
        var amountCrs = balanceCrs - reserveCrs;
        var amountSrc = balanceSrc - reserveSrc;
        var totalSupply = TotalSupply;

        UInt256 liquidity;
        
        if (totalSupply == 0)
        {
            liquidity = Sqrt(amountCrs * amountSrc) - MinimumLiquidity;
            MintTokensExecute(Address.Zero, MinimumLiquidity);
        }
        else
        {
            var amountCrsLiquidity = amountCrs * totalSupply / reserveCrs;
            var amountSrcLiquidity = amountSrc * totalSupply / reserveSrc;
            liquidity = amountCrsLiquidity > amountSrcLiquidity ? amountSrcLiquidity : amountCrsLiquidity;
        }
    
        Assert(liquidity > 0, "OPDEX: INSUFFICIENT_LIQUIDITY");
    
        MintTokensExecute(to, liquidity);
        UpdateReserves(balanceCrs, balanceSrc);

        Log(new MintLog
        {
            AmountCrs = amountCrs, 
            AmountSrc = amountSrc, 
            AmountLpt = liquidity,
            Sender = Message.Sender,
            To = to
        });

        return liquidity;
    }

    /// <inheritdoc />
    public abstract UInt256[] Burn(Address to);
    
    protected UInt256[] BurnExecute(Address to, UInt256 liquidity)
    {
        var address = Address;
        var token = Token;
        var balanceCrs = Balance;
        var balanceSrc = GetSrcBalance(token, address);
        var totalSupply = TotalSupply;
        var amountCrs = (ulong)(liquidity * balanceCrs / totalSupply);
        var amountSrc = liquidity * balanceSrc / totalSupply;
        
        Assert(amountCrs > 0 && amountSrc > 0, "OPDEX: INSUFFICIENT_LIQUIDITY_BURNED");
        
        BurnTokensExecute(address, liquidity);
        SafeTransfer(to, amountCrs);
        SafeTransferTo(token, to, amountSrc);
        
        balanceCrs = Balance;
        balanceSrc = GetSrcBalance(token, address);
        
        UpdateReserves(balanceCrs, balanceSrc);
        
        Log(new BurnLog
        {
            AmountCrs = amountCrs, 
            AmountSrc = amountSrc, 
            AmountLpt = liquidity,
            Sender = Message.Sender, 
            To = to
        });
        
        return new [] {amountCrs, amountSrc};
    }
    
    /// <inheritdoc />
    public abstract void Swap(ulong amountCrsOut, UInt256 amountSrcOut, Address to, byte[] data);

    protected void SwapExecute(ulong amountCrsOut, UInt256 amountSrcOut, Address to, byte[] data)
    {
        var reserveCrs = ReserveCrs;
        var reserveSrc = ReserveSrc;
        var token = Token;
    
        Assert(amountCrsOut > 0 || amountSrcOut > 0, "OPDEX: INVALID_OUTPUT_AMOUNT");
        Assert(amountCrsOut < reserveCrs && amountSrcOut < reserveSrc, "OPDEX: INSUFFICIENT_LIQUIDITY");
        Assert(to != token && to != Address, "OPDEX: INVALID_TO");
    
        SafeTransfer(to, amountCrsOut);
        SafeTransferTo(token, to, amountSrcOut);

        // Optional callback
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

        var fee = TransactionFee;
        const uint feeOffset = 1_000;

        var balanceCrsAdjusted = checked(balanceCrs * feeOffset) - checked(amountCrsIn * fee);
        var balanceSrcAdjusted = (balanceSrc * feeOffset) - (amountSrcIn * fee);
        
        Assert(balanceCrsAdjusted * balanceSrcAdjusted >= reserveCrs * reserveSrc * (feeOffset * feeOffset), "OPDEX: INSUFFICIENT_INPUT_AMOUNT");

        UpdateReserves(balanceCrs, balanceSrc);
        
        Log(new SwapLog 
        { 
            AmountCrsIn = amountCrsIn, 
            AmountCrsOut = amountCrsOut, 
            AmountSrcIn = amountSrcIn,
            AmountSrcOut = amountSrcOut, 
            Sender = Message.Sender, 
            To = to
        });
    }

    /// <inheritdoc />
    public abstract void Skim(Address to);
    
    protected void SkimExecute(Address to)
    {
        var token = Token;
        var balanceSrc = GetSrcBalance(token, Address) - ReserveSrc;
        var balanceCrs = Balance - ReserveCrs;
    
        SafeTransfer(to, balanceCrs);
        SafeTransferTo(token, to, balanceSrc);
    }

    /// <inheritdoc />
    public abstract void Sync();
    
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
        
        Log(new ApprovalLog { Owner = Message.Sender, Spender = spender, Amount = amount, OldAmount = currentAmount});
        
        return true;
    }
    
    /// <inheritdoc />
    public bool TransferFrom(Address from, Address to, UInt256 amount)
    {
        var allowance = Allowance(from, Message.Sender);

        if (allowance < amount) return false;
        
        SetAllowance(from, Message.Sender, allowance - amount);
        
        return TransferTokensExecute(from, to, amount);
    }
    
    protected bool TransferTokensExecute(Address from, Address to, UInt256 amount)
    {
        var fromBalance = GetBalance(from);

        if (fromBalance < amount) return false;
        
        SetBalance(from, fromBalance - amount);
        SetBalance(to, GetBalance(to) + amount);
        
        Log(new TransferLog { From = from,  To = to,  Amount = amount });
        
        return true;
    }
    
    protected void MintTokensExecute(Address to, UInt256 amount)
    {
        TotalSupply += amount;

        SetBalance(to, GetBalance(to) + amount);

        Log(new TransferLog { From = Address.Zero,  To = to,  Amount = amount });
    }
    
    private void BurnTokensExecute(Address from, UInt256 amount)
    {
        TotalSupply -= amount;

        SetBalance(from, GetBalance(from) - amount);
        
        Log(new TransferLog{ From = from, To = Address.Zero,  Amount = amount });
    }

    protected void UpdateReserves(ulong balanceCrs, UInt256 balanceSrc)
    {
        ReserveCrs = balanceCrs;
        ReserveSrc = balanceSrc;
        
        Log(new ReservesLog { ReserveCrs = balanceCrs, ReserveSrc = balanceSrc });
    }

    protected void UpdateKLast()
    {
        KLast = ReserveCrs * ReserveSrc;
    }

    protected void ResetKLast()
    {
        KLast = 0;
    }
    
    private static UInt256 Sqrt(UInt256 value)
    {
        if (value == 0) return 0;
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
    
    protected UInt256 GetSrcBalance(Address token, Address owner)
    {
        var balanceResponse = Call(token, 0, nameof(GetBalance), new object[] {owner});
    
        Assert(balanceResponse.Success, "OPDEX: INVALID_BALANCE");
    
        return (UInt256)balanceResponse.ReturnValue;
    }

    protected void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, nameof(TransferTo), new object[] {to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }
    
    protected void SafeTransferFrom(Address token, Address from, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, nameof(TransferFrom), new object[] {from, to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_FROM");
    }
    
    protected UInt256 CalculateFee()
    {
        var kLast = KLast;
        
        if (kLast == 0) return 0;
        
        var rootK = Sqrt(ReserveCrs * ReserveSrc);
        var rootKLast = Sqrt(kLast);
        
        if (rootK <= rootKLast) return 0;
        
        var numerator = TotalSupply * (rootK - rootKLast);
        var denominator = (rootK * 5) + rootKLast;
        var liquidity = numerator / denominator;
        
        return liquidity;
    }

    private void SafeTransfer(Address to, ulong amount)
    {
        if (amount == 0) return;
        
        Assert(Transfer(to, amount).Success, "OPDEX: INVALID_TRANSFER");
    }
    
    protected void EnsureUnlocked()
    {
        Assert(!Locked, "OPDEX: LOCKED");
        Locked = true;
    }

    protected void Unlock() => Locked = false;
}

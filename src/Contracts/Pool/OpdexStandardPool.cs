using Stratis.SmartContracts;

public class OpdexStandardPool : StandardToken, IOpdexStandardPool
{
    private const ulong MinimumLiquidity = 1000;

    public OpdexStandardPool(ISmartContractState contractState, Address token) 
        : base(contractState)
    {
        Token = token;
    }
    
    public override void Receive() => base.Receive();
    
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
    public byte[][] Reserves => new [] { Serializer.Serialize(ReserveCrs), Serializer.Serialize(ReserveSrc) };
        
    /// <inheritdoc />
    public virtual UInt256 Mint(Address to)
    {
        EnsureUnlocked();

        var liquidity = MintExecute(to);
        
        Unlock();
    
        return liquidity;
    }
        
    /// <inheritdoc />
    public virtual UInt256[] Burn(Address to)
    {
        EnsureUnlocked();
        
        var amounts = BurnExecute(to,  GetBalance(Address));
    
        Unlock();

        return amounts;
    }
        
    /// <inheritdoc />
    public virtual void Skim(Address to)
    {
        EnsureUnlocked();
    
        SkimExecute(to);
    
        Unlock();
    }
    
    /// <inheritdoc />
    public virtual void Sync()
    {
        EnsureUnlocked();
    
        UpdateReserves(Balance, GetSrcBalance(Token, Address));
    
        Unlock();
    }
        
    /// <inheritdoc />
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
    
        UpdateReserves(balanceCrs, balanceSrc);
        
        Log(new OpdexSwapEvent 
        { 
            AmountCrsIn = amountCrsIn, 
            AmountCrsOut = amountCrsOut, 
            AmountSrcIn = amountSrcIn.ToString(),
            AmountSrcOut = amountSrcOut.ToString(), 
            Sender = Message.Sender, 
            To = to
        });
    
        Unlock();
    }

    protected void SkimExecute(Address to)
    {
        var token = Token;
        var balanceSrc = GetSrcBalance(token, Address) - ReserveSrc;
        var balanceCrs = Balance - ReserveCrs;
    
        SafeTransfer(to, balanceCrs);
        SafeTransferTo(token, to, balanceSrc);
    }

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
    
        KLast = ReserveCrs * ReserveSrc;

        Log(new OpdexMintEvent
        {
            AmountCrs = amountCrs, 
            AmountSrc = amountSrc.ToString(), 
            Sender = Message.Sender
        });

        return liquidity;
    }
    
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
        
        KLast = ReserveCrs * ReserveSrc;
        
        Log(new OpdexBurnEvent
        {
            AmountCrs = amountCrs, 
            AmountSrc = amountSrc.ToString(), 
            Sender = Message.Sender, 
            To = to
        });
        
        return new [] {amountCrs, amountSrc};
    }
    
    protected static UInt256 Sqrt(UInt256 value)
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
    
    protected void EnsureUnlocked()
    {
        Assert(!Locked, "OPDEX: LOCKED");
        Locked = true;
    }

    protected void Unlock() => Locked = false;
    
    protected UInt256 GetSrcBalance(Address token, Address owner)
    {
        var balanceResponse = Call(token, 0, "GetBalance", new object[] {owner});
    
        Assert(balanceResponse.Success, "OPDEX: INVALID_BALANCE");
    
        return (UInt256)balanceResponse.ReturnValue;
    }
    
    protected void UpdateReserves(ulong balanceCrs, UInt256 balanceSrc)
    {
        ReserveCrs = balanceCrs;
        ReserveSrc = balanceSrc;
        
        Log(new OpdexSyncEvent
        {
            ReserveCrs = balanceCrs, 
            ReserveSrc = balanceSrc.ToString()
        });
    }
    
    protected void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, "TransferTo", new object[] {to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_TO");
    }
    
    protected void SafeTransferFrom(Address token, Address from, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, "TransferFrom", new object[] {from, to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_FROM");
    }

    private void SafeTransfer(Address to, ulong amount)
    {
        if (amount == 0) return;
        
        Assert(Transfer(to, amount).Success, "OPDEX: INVALID_TRANSFER");
    }
}

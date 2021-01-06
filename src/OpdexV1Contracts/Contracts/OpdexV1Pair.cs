using Stratis.SmartContracts;

public class OpdexV1PairDraft : SmartContract
{
    private const ulong MinimumLiquidity = 10;
    public OpdexV1PairDraft(ISmartContractState smartContractState, Address token) : base(smartContractState)
    {
        Factory = Message.Sender;
        Token = token;
    }

    #region Properties
    
    /// <summary>
    /// 
    /// </summary>
    public Address Factory
    {
        get => PersistentState.GetAddress(nameof(Factory));
        private set => PersistentState.SetAddress(nameof(Factory), value);
    }
    
    /// <summary>
    /// 
    /// </summary>
    public Address Token
    {
        get => PersistentState.GetAddress(nameof(Token));
        private set => PersistentState.SetAddress(nameof(Token), value);
    }

    /// <summary>
    /// 
    /// </summary>
    public ulong ReserveCrs
    {
        get => PersistentState.GetUInt32(nameof(ReserveCrs));
        private set => PersistentState.SetUInt64(nameof(ReserveCrs), value);
    }
    
    /// <summary>
    /// 
    /// </summary>
    public ulong ReserveToken
    {
        get => PersistentState.GetUInt64(nameof(ReserveToken));
        private set => PersistentState.SetUInt64(nameof(ReserveToken), value);
    }
    
    /// <summary>
    /// 
    /// </summary>
    public ulong LastBlock
    {
        get => PersistentState.GetUInt64(nameof(LastBlock));
        private set => PersistentState.SetUInt64(nameof(LastBlock), value);
    }

    /// <summary>
    /// 
    /// </summary>
    public ulong KLast
    {
        get => PersistentState.GetUInt64(nameof(KLast));
        private set => PersistentState.SetUInt64(nameof(KLast), value);
    }
    
    /// <summary>
    /// 
    /// </summary>
    public ulong TotalSupply 
    {
        get => PersistentState.GetUInt64(nameof(TotalSupply));
        private set => PersistentState.SetUInt64(nameof(TotalSupply), value);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="address"></param>
    /// <returns></returns>
    public ulong GetBalance(Address address) 
        => PersistentState.GetUInt64($"Balance:{address}");

    /// <summary>
    /// 
    /// </summary>
    /// <param name="address"></param>
    /// <param name="value"></param>
    private void SetBalance(Address address, ulong value) 
        => PersistentState.SetUInt64($"Balance:{address}", value);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="spender"></param>
    /// <returns></returns>
    public ulong GetAllowance(Address owner, Address spender) 
        => PersistentState.GetUInt64($"Allowance:{owner}:{spender}");
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="spender"></param>
    /// <param name="value"></param>
    private void SetAllowance(Address owner, Address spender, ulong value) 
        => PersistentState.SetUInt64($"Allowance:{owner}:{spender}", value);
    
    #endregion

    #region External Methods
    
    /// <summary>
    /// Returns the pools reserves for this pairing
    /// </summary>
    /// <returns>Reserves</returns>
    public Reserves GetReserves() 
        => new Reserves { ReserveCrs = ReserveCrs, ReserveToken = ReserveToken, LastBlock = LastBlock };
    
    /// <summary>
    /// Mints new tokens for providing liquidity, should be called from the Factory contract only
    /// </summary>
    /// <param name="to">The address to transfer the minted tokens to</param>
    /// <param name="from">The original callers address</param>
    public void Mint(Address to)
    {
        var reserves = GetReserves();
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(Token, Address);
        var amountCrs = checked(balanceCrs - reserves.ReserveCrs);
        var amountToken = checked(balanceToken - reserves.ReserveToken);
        var totalSupply = TotalSupply;
        ulong liquidity;
        
        if (totalSupply == 0)
        {
            liquidity = checked(Sqrt(checked(amountCrs * TotalSupply)) - MinimumLiquidity);
            MintExecute(Address.Zero, MinimumLiquidity);
        }
        else
        {
            var amountCrsLiquidity = checked(amountCrs * TotalSupply) / ReserveCrs;
            var amountTokenLiquidity = checked(amountToken * TotalSupply) / ReserveToken;
            liquidity = amountCrsLiquidity > amountTokenLiquidity ? amountTokenLiquidity : amountCrsLiquidity;
        }
        
        Assert(liquidity > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        MintFee(reserves.ReserveCrs, reserves.ReserveToken);
        MintExecute(to, liquidity);
        Update(balanceCrs, balanceToken);
        Log(new MintEvent { AmountCrs = amountCrs, AmountToken = amountToken, Sender = Message.Sender });
    }

    /// <summary>
    /// Burns pool tokens when removing liquidity, should be called from the Factory contract only
    /// </summary>
    /// <param name="to">The address to transfer CRS/SRC tokens to</param>
    public void Burn(Address to)
    {
        var reserves = GetReserves();
        var token = Token;
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(token, Address);
        var liquidity = GetBalance(Address);
        var totalSupply = TotalSupply;
        var amountCrs = checked(liquidity * balanceCrs) / totalSupply;
        var amountToken = checked(liquidity * balanceToken) / totalSupply;
        
        Assert(amountCrs > 0 && amountToken > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY_BURNED");
        MintFee(reserves.ReserveCrs, reserves.ReserveToken);
        BurnExecute(Address, liquidity);
        SafeTransfer(to, amountCrs);
        SafeTransferTo(token, to, amountToken);
        
        balanceCrs = Balance;
        balanceToken = GetSrcBalance(token, Address);
        
        Update(balanceCrs, balanceToken);

        KLast = checked(checked(reserves.ReserveCrs - balanceCrs) * checked(reserves.ReserveToken - balanceToken));
        
        Log(new BurnEvent { Sender = Message.Sender, To = to, AmountCrs = amountCrs, AmountToken = amountToken });
    }

    /// <summary>
    /// Swaps tokens, should be called from the Factory contract only
    /// </summary>
    /// <param name="amountCrsOut"></param>
    /// <param name="amountTokenOut"></param>
    /// <param name="to"></param>
    public void Swap(ulong amountCrsOut, ulong amountTokenOut, Address to)
    {
        Assert(amountCrsOut > 0 || amountTokenOut > 0, "OpdexV1: INVALID_OUTPUT_AMOUNT");
        
        var reserves = GetReserves();
        Assert(amountCrsOut < reserves.ReserveCrs && amountTokenOut < reserves.ReserveToken, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        
        var token = Token;
        Assert(to != token, "OpdexV1: INVALID_TO");
        
        if (amountCrsOut > 0) SafeTransfer(to, amountCrsOut);
        if (amountTokenOut > 0) SafeTransferTo(token, to, amountTokenOut);
        
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(token, Address);
        var amountCrsIn = balanceCrs > checked(reserves.ReserveCrs - amountCrsOut) 
            ? checked(balanceCrs - checked(reserves.ReserveCrs - amountCrsOut)) 
            : 0;
        var amountTokenIn = balanceToken > checked(reserves.ReserveToken - amountTokenOut) 
            ? checked(balanceToken - checked(reserves.ReserveToken - amountTokenOut)) 
            : 0;
        
        Assert(amountCrsIn > 0 || amountTokenIn > 0, "OpdexV1: INSUFFICIENT_INPUT_AMOUNT");
        
        var balanceCrsAdjusted = checked(checked(balanceCrs * 1_000) - checked(amountCrsIn * 3));
        var balanceTokenAdjusted = checked(checked(balanceToken * 1_000) - checked(amountTokenIn * 3));
        
        Assert(checked(balanceCrsAdjusted * balanceTokenAdjusted) >= checked(checked(reserves.ReserveCrs * reserves.ReserveToken) * 1_000_000));
        Update(balanceCrs, balanceToken);
        Log(new SwapEvent { AmountCrsIn = amountCrsIn, AmountCrsOut = amountCrsOut, 
            AmountTokenIn = amountTokenIn, AmountTokenOut = amountTokenOut, Sender = Message.Sender, To = to });
    }
    
    /// <summary>
    /// Forces this contracts balances to match reserves
    /// </summary>
    /// <param name="to">The address to send the difference to</param>
    public void Skim(Address to)
    {
        var token = Token;
        var balanceToken = checked(GetSrcBalance(token, Address) - ReserveToken);
        var balanceCrs = checked(Balance - ReserveCrs);
        SafeTransfer(to, balanceToken);
        SafeTransferTo(token, to, balanceCrs);
    }

    /// <summary>
    /// Forces the reserves amounts to match this contracts balances
    /// </summary>
    public void Sync() 
        => Update(Balance, GetSrcBalance(Token, Address));
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="spender"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool Approve(Address spender, ulong value) 
        => ApproveExecute(Message.Sender, spender, value);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="to"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TransferTo(Address to, ulong value) 
        => TransferExecute(Message.Sender, to, value);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TransferFrom(Address from, Address to, ulong value)
    {
        var allowance = GetAllowance(from, Message.Sender);
        if (allowance > 0) SetAllowance(from, Message.Sender, checked(allowance - value));
        return TransferExecute(from, to, value);
    }
    
    #endregion

    #region Private Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="balanceCrs"></param>
    /// <param name="balanceToken"></param>
    private void Update(ulong balanceCrs, ulong balanceToken)
    {
        ReserveCrs = balanceCrs;
        ReserveToken = balanceToken;
        LastBlock = Block.Number;
        
        Log(new SyncEvent { ReserveCrs = balanceCrs, ReserveToken = balanceToken });
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="reserveCrs"></param>
    /// <param name="reserveToken"></param>
    /// <param name="feeTo"></param>
    private void MintFee(ulong reserveCrs, ulong reserveToken)
    {
        var kLast = KLast;
        
        if (kLast == 0) return;
        
        var rootK = checked(Sqrt(reserveCrs) * reserveToken);
        var rootKLast = Sqrt(kLast);

        if (rootK <= rootKLast) return;
            
        var numerator = checked(TotalSupply * checked(rootK - rootKLast));
        var denominator = checked(checked(rootK * 5) + rootKLast);
        var liquidity = numerator / denominator;

        if (liquidity == 0) return;
        
        MintExecute(GetFeeTo(), liquidity);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="to"></param>
    /// <param name="value"></param>
    private void MintExecute(Address to, ulong value)
    {
        var balance = GetBalance(to);
        TotalSupply = checked(TotalSupply + value);
        SetBalance(to, checked(balance + value));
        Log(new TransferEvent { From = Address.Zero, To = to, Value = value });
    }

    /// <summary>
    /// Gets the token balance of an address and validates the response
    /// </summary>
    /// <param name="token">The src token contract address</param>
    /// <param name="owner">The address to get the balance for</param>
    /// <returns>ulong balance</returns>
    private ulong GetSrcBalance(Address token, Address owner)
    {
        var balanceResponse = Call(token, 0, "GetBalance", new object[] {owner});
        Assert(balanceResponse.Success, "OpdexV1: INVALID_BALANCE");
        return (ulong)balanceResponse.ReturnValue;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private Address GetFeeTo()
    {
        var feeToResponse = Call(Factory, 0, "GetFeeTo");
        var feeTo = (Address)feeToResponse.ReturnValue;
        Assert(feeToResponse.Success && feeTo != Address.Zero, "OpdexV1: INVALID_FACTORY_ADDRESS");
        return feeTo;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="from"></param>
    /// <param name="value"></param>
    private void BurnExecute(Address from, ulong value)
    {
        var balance = GetBalance(from);
        SetBalance(from, checked(balance - value));
        TotalSupply = checked(TotalSupply - value);
        Log(new TransferEvent { From = from, To = Address.Zero, Value = value });
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="spender"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    private bool ApproveExecute(Address owner, Address spender, ulong value)
    {
        SetAllowance(owner, spender, value);
        Log(new ApprovalEvent { Owner = owner, Spender = spender, Value = value });
        return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    private bool TransferExecute(Address from, Address to, ulong value)
    {
        var fromBalance = GetBalance(from);
        SetBalance(from, checked(fromBalance - value));
        var toBalance = GetBalance(to);
        SetBalance(to, checked(toBalance + value));
        Log(new TransferEvent {From = from, To = to, Value = value});
        return true;
    }
    
    /// <summary>
    /// Transfers CRS tokens to an address
    /// </summary>
    /// <param name="to">The address to send tokens to</param>
    /// <param name="amount">The amount to send</param>
    private void SafeTransfer(Address to, ulong amount)
    {
        var result = Transfer(to, amount);
        Assert(result.Success, "OpdexV1: INVALID_TRANSFER");
    }
    
    /// <summary>
    /// Calls SRC TransferTo method and validates the response
    /// </summary>
    /// <param name="token">The src token contract address</param>
    /// <param name="to">The address to transfer tokens to</param>
    /// <param name="amount">The amount to transfer</param>
    private void SafeTransferTo(Address token, Address to, ulong amount)
    {
        var result = Call(token, 0, "TransferTo", new object[] {to, amount});
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_FROM");
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="y"></param>
    /// <returns></returns>
    public static ulong Sqrt(ulong y)
    {
        ulong z = 0;
        if (y > 3) 
        {
            z = y;
            var x = y / 2 + 1;
            while (x < z) 
            {
                z = x;
                x = (y / x + x) / 2;
            }
        } 
        else if (y != 0) {
            z = 1;
        }
        return z;
    }
    
    #endregion

    #region Models
    
    /// <summary>
    /// 
    /// </summary>
    public struct Reserves
    {
        public ulong ReserveCrs;
        public ulong ReserveToken;
        public ulong LastBlock;
    }

    /// <summary>
    /// 
    /// </summary>
    public struct SyncEvent
    {
        public ulong ReserveCrs;
        public ulong ReserveToken;
    }
    
    /// <summary>
    /// 
    /// </summary>
    public struct MintEvent
    {
        [Index] public Address Sender;
        public ulong AmountCrs;
        public ulong AmountToken;
    }
    
    /// <summary>
    /// 
    /// </summary>
    public struct BurnEvent
    {
        [Index] public Address Sender;
        [Index] public Address To;
        public ulong AmountCrs;
        public ulong AmountToken;
    }
    
    /// <summary>
    /// 
    /// </summary>
    public struct SwapEvent
    {
        [Index] public Address Sender;
        [Index] public Address To;
        public ulong AmountCrsIn;
        public ulong AmountTokenIn;
        public ulong AmountCrsOut;
        public ulong AmountTokenOut;
    }
    
    /// <summary>
    /// 
    /// </summary>
    public struct ApprovalEvent
    {
        [Index] public Address Owner;
        [Index] public Address Spender;
        public ulong Value;
    }

    /// <summary>
    /// 
    /// </summary>
    public struct TransferEvent
    {
        [Index] public Address From;
        [Index] public Address To;
        public ulong Value;
    }

    #endregion
}
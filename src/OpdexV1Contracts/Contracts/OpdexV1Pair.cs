using Stratis.SmartContracts;

[Deploy]
public class OpdexV1Pair : OpdexV1SRC
{
    private const ulong MinimumLiquidity = 1000;
    public OpdexV1Pair(ISmartContractState smartContractState, Address token) : base(smartContractState)
    {
        Factory = Message.Sender;
        Token = token;
    }
    
    /// <summary>
    /// Only accept CRS sent by the Factory contract
    /// </summary>
    public override void Receive()
    {
        Assert(Message.Sender == Factory, "OpdexV1: UNACCEPTED_CRS");
        base.Receive();
    }

    #region Properties
    
    public Address Factory
    {
        get => PersistentState.GetAddress(nameof(Factory));
        private set => PersistentState.SetAddress(nameof(Factory), value);
    }
    
    public Address Token
    {
        get => PersistentState.GetAddress(nameof(Token));
        private set => PersistentState.SetAddress(nameof(Token), value);
    }

    // Todo: Broken down token amounts or UInt256 support
    public ulong ReserveCrs
    {
        get => PersistentState.GetUInt32(nameof(ReserveCrs));
        private set => PersistentState.SetUInt64(nameof(ReserveCrs), value);
    }
    
    // Todo: Broken down token amounts or UInt256 support
    public ulong ReserveToken
    {
        get => PersistentState.GetUInt64(nameof(ReserveToken));
        private set => PersistentState.SetUInt64(nameof(ReserveToken), value);
    }
    
    // Todo: UNIX timestamp if possible
    public ulong LastBlock
    {
        get => PersistentState.GetUInt64(nameof(LastBlock));
        private set => PersistentState.SetUInt64(nameof(LastBlock), value);
    }
    
    #endregion

    #region External Methods
    
    /// <summary>
    /// Returns the pools reserves for this pairing
    /// </summary>
    /// <returns>Reserves</returns>
    public Reserves GetReserves()
    {
        return new Reserves
        {
            ReserveCrs = ReserveCrs,
            ReserveToken = ReserveToken,
            LastBlock = LastBlock
        };
    }
    
    /// <summary>
    /// Mints new tokens for providing liquidity, should be called from the Factory contract only
    /// </summary>
    /// <param name="to">The address to transfer the minted tokens to</param>
    /// <param name="from">The original callers address</param>
    public void Mint(Address to, Address from)
    {
        Authorize();
        var reserves = GetReserves();
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(Token, Address);
        // funds are already sent, find out how much by subtracting last recorded reserves from the current balances
        var amountCrs = SafeMath.Sub(balanceCrs, reserves.ReserveCrs);
        var amountToken = SafeMath.Sub(balanceToken, reserves.ReserveToken);
        var totalSupply = TotalSupply;
        ulong liquidity;
        
        if (totalSupply == 0)
        {
            // Todo: This is flawed and temporary intentionally
            // uint64 * uint64 will result in overflows, need uint128 && uint256 support
            // or break this down further (maybe not even possible with SC limitations)
            // squareRoot(amountCrs * TotalSupply) - MinimumLiquidity
            liquidity = SafeMath.Sub(SafeMath.Sqrt(SafeMath.Mul(amountCrs, TotalSupply)), MinimumLiquidity);
            MintExecute(Address.Zero, MinimumLiquidity);
        }
        else
        {
            // Todo: This is flawed and temporary intentionally
            // uint64 * uint64 will result in overflows, need uint128 && uint256 support
            // or break this down further (maybe not even possible with SC limitations)
            var amountCrsLiquidity = SafeMath.Div(SafeMath.Mul(amountCrs, TotalSupply), ReserveCrs);
            var amountTokenLiquidity = SafeMath.Div(SafeMath.Mul(amountToken, TotalSupply), ReserveToken);
            liquidity = amountCrsLiquidity > amountTokenLiquidity ? amountTokenLiquidity : amountCrsLiquidity;
        }
        
        Assert(liquidity > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        
        MintExecute(to, liquidity);
        
        Update(balanceCrs, balanceToken, ReserveCrs, ReserveToken);
        
        Log(new MintEvent
        {
            AmountCrs = amountCrs, 
            AmountToken = amountToken, 
            Sender = from
        });
    }

    /// <summary>
    /// Burns pool tokens when removing liquidity, should be called from the Factory contract only
    /// </summary>
    /// <param name="to">The address to transfer CRS/SRC tokens to</param>
    public void Burn(Address to, Address from)
    {
        Authorize();
        var reserves = GetReserves();
        var token = Token;
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(token, Address);
        var liquidity = GetBalance(Address);
        var totalSupply = TotalSupply;
        var amountCrs = SafeMath.Div(SafeMath.Mul(liquidity, balanceCrs), totalSupply);
        var amountToken = SafeMath.Div(SafeMath.Mul(liquidity, balanceToken), totalSupply);
        
        Assert(amountCrs > 0 && amountToken > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY_BURNED");
        
        BurnExecute(Address, liquidity);
        
        SafeTransfer(to, amountCrs);
        SafeTransferTo(token, to, amountToken);
        
        balanceCrs = Balance;
        balanceToken = GetSrcBalance(token, Address);
        
        Update(balanceCrs, balanceToken, reserves.ReserveCrs, reserves.ReserveToken);
        
        // KLast
        
        Log(new BurnEvent
        {
            Sender = from, 
            To = to, 
            AmountCrs = amountCrs, 
            AmountToken = amountToken
        });
    }

    /// <summary>
    /// Swaps tokens, should be called from the Factory contract only
    /// </summary>
    /// <param name="amountCrsOut"></param>
    /// <param name="amountTokenOut"></param>
    /// <param name="to"></param>
    /// <param name="data"></param>
    /// <param name="data"></param>
    public void Swap(ulong amountCrsOut, ulong amountTokenOut, Address to, byte[] data, Address from)
    {
        Authorize();
        Assert(amountCrsOut > 0 || amountTokenOut > 0, "OpdexV1: INVALID_OUTPUT_AMOUNT");
        
        var reserves = GetReserves();
        Assert(amountCrsOut < reserves.ReserveCrs && amountTokenOut < reserves.ReserveToken, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        
        var token = Token;
        Assert(to != token, "OpdexV1: INVALID_TO");
        
        if (amountCrsOut > 0) SafeTransfer(to, amountCrsOut);
        if (amountTokenOut > 0) SafeTransferTo(token, to, amountTokenOut);
        
        // if data.length
        
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(token, Address);
        var amountCrsIn = balanceCrs > SafeMath.Sub(reserves.ReserveCrs, amountCrsOut) 
            ? SafeMath.Sub(balanceCrs, SafeMath.Sub(reserves.ReserveCrs,amountCrsOut)) 
            : 0;
        
        var amountTokenIn = balanceToken > SafeMath.Sub(reserves.ReserveToken, amountTokenOut) 
            ? SafeMath.Sub(balanceToken, SafeMath.Sub(reserves.ReserveToken, amountTokenOut)) 
            : 0;
        
        Assert(amountCrsIn > 0 || amountTokenIn > 0, "OpdexV1: INSUFFICIENT_INPUT_AMOUNT");
        
        var balanceCrsAdjusted = SafeMath.Sub(SafeMath.Mul(balanceCrs, 1000), SafeMath.Mul(amountCrsIn, 3));
        var balanceTokenAdjusted = SafeMath.Sub(SafeMath.Mul(balanceToken, 1000), SafeMath.Mul(amountTokenIn, 3));
        
        Assert(SafeMath.Mul(balanceCrsAdjusted, balanceTokenAdjusted) >= SafeMath.Mul(SafeMath.Mul(reserves.ReserveCrs, reserves.ReserveToken), 1_000_000));
        
        Update(balanceCrs, balanceToken, reserves.ReserveCrs, reserves.ReserveToken);
        
        Log(new SwapEvent
        {
            AmountCrsIn = amountCrsIn, 
            AmountCrsOut = amountCrsOut, 
            AmountTokenIn = amountTokenIn, 
            AmountTokenOut = amountTokenOut, 
            Sender = from, 
            To = to
        });
    }
    
    /// <summary>
    /// Forces this contracts balances to match reserves
    /// </summary>
    /// <param name="to">The address to send the difference to</param>
    public void Skim(Address to)
    {
        var token = Token;
        var balanceToken = SafeMath.Sub(GetSrcBalance(token, Address), ReserveToken);
        var balanceCrs = SafeMath.Sub(Balance, ReserveCrs);
        
        SafeTransfer(to, balanceToken);
        SafeTransferTo(token, to, balanceCrs);
    }

    /// <summary>
    /// Forces the reserves amounts to match this contracts balances
    /// </summary>
    public void Sync() => Update(Balance, GetSrcBalance(Token, Address), ReserveCrs, ReserveToken);
    
    #endregion
    
    #region Private Methods

    /// <summary>
    /// Verify that the caller is the Factory.
    /// </summary>
    private void Authorize() => Assert(Message.Sender == Factory, "OpdexV1: FORBIDDEN");

    /// <summary>
    /// 
    /// </summary>
    /// <param name="balanceCrs"></param>
    /// <param name="balanceToken"></param>
    /// <param name="reserveCrs"></param>
    /// <param name="reserveToken"></param>
    private void Update(ulong balanceCrs, ulong balanceToken, ulong reserveCrs, ulong reserveToken)
    {
        ReserveCrs = balanceCrs;
        ReserveToken = balanceToken;
        LastBlock = Block.Number;
        
        // Todo: Double check this, should ReserveCrs be set to balanceCrs or reserveCrs?
        Log(new SyncEvent
        {
            ReserveCrs = balanceCrs, 
            ReserveToken = reserveToken
        });
    }

    /// <summary>
    /// 
    /// </summary>
    private void MintFee()
    {
        // Todo: Implement mint fee
        // Would Mint 1/6 of the latest swap transactions fee, applied to Opdex FeeTo address
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
    /// Calls SRC TransferFrom method and validates the response.
    /// </summary>
    /// <param name="token">The src token contract address</param>
    /// <param name="from">The approvers address</param>
    /// <param name="to">Address to transfer tokens to</param>
    /// <param name="amount">The amount to transfer</param>
    private void SafeTransferFrom(Address token, Address from, Address to, ulong amount)
    {
        var result = Call(token, 0, "TransferFrom", new object[] {from, to, amount});
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_FROM");
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
        return (ulong) balanceResponse.ReturnValue;
    }
    
    #endregion

    #region Models

    public struct SyncEvent
    {
        public ulong ReserveCrs;
        public ulong ReserveToken;
    }
    
    public struct MintEvent
    {
        [Index] public Address Sender;
        public ulong AmountCrs;
        public ulong AmountToken;
    }
    
    public struct BurnEvent
    {
        [Index] public Address Sender;
        [Index] public Address To;
        public ulong AmountCrs;
        public ulong AmountToken;
    }
    
    public struct SwapEvent
    {
        [Index] public Address Sender;
        [Index] public Address To;
        public ulong AmountCrsIn;
        public ulong AmountTokenIn;
        public ulong AmountCrsOut;
        public ulong AmountTokenOut;
    }
    
    public struct Reserves
    {
        public ulong ReserveCrs;
        public ulong ReserveToken;
        // Preferably LastBlockTimestamp - UNIX timestamp
        public ulong LastBlock;
    }

    #endregion
}
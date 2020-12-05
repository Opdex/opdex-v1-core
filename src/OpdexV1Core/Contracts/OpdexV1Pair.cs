using Stratis.SmartContracts;

[Deploy]
// Todo: This contract may need to be included in OpdexV1Router.cs
public class OpdexV1Pair : OpdexV1SRC
{
    public OpdexV1Pair(ISmartContractState smartContractState, Address token) : base(smartContractState)
    {
        Factory = Message.Sender;
        // Todo: Get decimals/stratsPer / get uint256 support
        Token = token;
    }
    
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

    public uint TokenSats
    {
        get => PersistentState.GetUInt32(nameof(TokenSats));
        private set => PersistentState.SetUInt32(nameof(TokenSats), value);
    }
    
    // Probably needs to be TokenAmounts Struct or wait for uint256 support
    public ulong ReserveCrs
    {
        get => PersistentState.GetUInt32(nameof(ReserveCrs));
        private set => PersistentState.SetUInt64(nameof(ReserveCrs), value);
    }
    
    // Probably needs to be TokenAmounts Struct or wait for uint256 support
    public ulong ReserveToken
    {
        get => PersistentState.GetUInt64(nameof(ReserveToken));
        private set => PersistentState.SetUInt64(nameof(ReserveToken), value);
    }
    
    public ulong LastBlock
    {
        get => PersistentState.GetUInt64(nameof(LastBlock));
        private set => PersistentState.SetUInt64(nameof(LastBlock), value);
    }
    
    #endregion

    #region External Methods
    
    public Reserves GetReserves()
    {
        return new Reserves
        {
            ReserveCrs = ReserveCrs,
            ReserveToken = ReserveToken,
            LastBlock = LastBlock
        };
    }
    
    public void Mint(Address to, Address from)
    {
        Authorize();
        var reserves = GetReserves();
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(Token, Address);
        // funds are already sent, find out how much by subtracting last recorded
        // reserves from the current balances
        var amountCrs = SafeMath.Sub(balanceCrs, reserves.ReserveCrs);
        var amountToken = SafeMath.Sub(balanceToken, reserves.ReserveToken);
        var totalSupply = TotalSupply;
        ulong liquidity = 0;
        
        if (totalSupply == 0)
        {
            // Todo: Update when safe math is updated for ulong * ulong 
            liquidity = 0;
        }
        else
        {
            // Todo: Update when safe math is updated for ulong * ulong 
            liquidity = 1;
        }
        
        Assert(liquidity > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        MintExecute(to, liquidity);
        
        Update(balanceCrs, balanceToken, ReserveCrs, ReserveToken);

        Log(new MintEvent
        {
            AmountCrs = amountCrs,
            AmountToken = amountToken,
            Sender = from // Should this be from address or to address?
        });
    }

    public void Burn()
    {
        Authorize();
        var reserves = GetReserves();
    }

    public void Swap()
    {
        Authorize();
        var reserves = GetReserves();
    }
    
    public void Skim(Address to)
    {
        var token = Token;
        var balanceToken = SafeMath.Sub(GetSrcBalance(token, Address), ReserveToken);
        var balanceCrs = SafeMath.Sub(Balance, ReserveCrs);
        SafeTransfer(to, balanceToken);
        SafeTransferTo(token, to, balanceCrs);
    }

    public void Sync()
    {
        Update(Balance, GetSrcBalance(Token, Address), ReserveCrs, ReserveToken);
    }
    
    #endregion
    
    #region Private Methods

    private void Authorize()
    {
        Assert(Message.Sender == Factory, "OpdexV1: FORBIDDEN");
    }

    private void Update(ulong balanceCrs, ulong balanceToken, ulong reserveCrs, ulong reserveToken)
    {
        
    }

    private void MintFee()
    {
        
    }
    
    private void SafeTransfer(Address to, ulong amount)
    {
        var result = Transfer(to, amount);
        Assert(result.Success, "OpdexV1: INVALID_TRANSFER");
    }
    
    private void SafeTransferTo(Address token, Address to, ulong amount)
    {
        var result = Call(token, 0, "TransferTo", new object[] {to, amount});
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_FROM");
    }

    private void SafeTransferFrom(Address token, Address from, Address to, ulong amount)
    {
        var result = Call(token, 0, "TransferFrom", new object[] {from, to, amount});
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_FROM");
    }

    private ulong GetSrcBalance(Address token, Address owner)
    {
        var balanceResponse = Call(token, 0, "GetBalance", new object[] {owner});
        Assert(balanceResponse.Success, "OpdexV1: INVALID_BALANCE");
        return (ulong) balanceResponse.ReturnValue;
    }
    
    #endregion

    #region Models
    
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
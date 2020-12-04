using Stratis.SmartContracts;

[Deploy]
// Todo: this contract itself needs to inherit a base Token type class
// Todo: This contract may need to be included in OpdexV1Router.cs
public class OpdexV1Pair : SmartContract
{
    public OpdexV1Pair(ISmartContractState smartContractState, Address token) : base(smartContractState)
    {
        Factory = Message.Sender;
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
    
    public void Mint()
    {
        Assert(Message.Sender == Factory, "OpdexV1: FORBIDDEN");
        var reserves = GetReserves();
    }

    public void Burn()
    {
        Assert(Message.Sender == Factory, "OpdexV1: FORBIDDEN");
        var reserves = GetReserves();
    }

    public void Swap()
    {
        Assert(Message.Sender == Factory, "OpdexV1: FORBIDDEN");
        var reserves = GetReserves();
    }
    
    #endregion
    
    #region Private Methods
    
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
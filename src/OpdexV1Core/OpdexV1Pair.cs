using Stratis.SmartContracts;

public class OpdexV1Pair : SmartContract
{
    public OpdexV1Pair(ISmartContractState smartContractState, Address tokenA, Address tokenB) : base(smartContractState)
    {
        Factory = Message.Sender;
        TokenA = tokenA;
        TokenB = tokenB;
    }

    public Address Factory
    {
        get => PersistentState.GetAddress(nameof(Factory));
        private set => PersistentState.SetAddress(nameof(Factory), value);
    }
    
    public Address TokenA
    {
        get => PersistentState.GetAddress(nameof(TokenA));
        private set => PersistentState.SetAddress(nameof(TokenA), value);
    }
    
    public Address TokenB
    {
        get => PersistentState.GetAddress(nameof(TokenB));
        private set => PersistentState.SetAddress(nameof(TokenB), value);
    }
    
    public ulong ReserveA
    {
        get => PersistentState.GetUInt32(nameof(ReserveA));
        private set => PersistentState.SetUInt64(nameof(ReserveA), value);
    }
    
    public ulong ReserveB
    {
        get => PersistentState.GetUInt64(nameof(ReserveB));
        private set => PersistentState.SetUInt64(nameof(ReserveB), value);
    }

    public struct MintEvent
    {
        [Index] public Address Sender;
        public ulong AmountA;
        public ulong AmountB;
    }
    
    public struct BurnEvent
    {
        [Index] public Address Sender;
        public ulong AmountA;
        public ulong AmountB;
        [Index] public ulong To;
    }

    public struct SwapEvent
    {
        [Index] public Address Sender;
        public ulong AmountAIn;
        public ulong AmountBIn;
        public ulong AmountAOut;
        public ulong AmountBOut;
        [Index] public Address To;
    }

    public void Mint()
    {
        
    }

    public void Burn()
    {
        
    }

    public void Swap()
    {
        
    }
}
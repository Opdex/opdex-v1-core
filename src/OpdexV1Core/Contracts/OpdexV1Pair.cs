using System.Reflection.PortableExecutable;
using Stratis.SmartContracts;

// Todo: this contract iteslf needs to inherit a base Token type class
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
    
    // Probably needs to be TokenAmounts Struct
    // To help with math computations without overflow
    public ulong ReserveA
    {
        get => PersistentState.GetUInt32(nameof(ReserveA));
        private set => PersistentState.SetUInt64(nameof(ReserveA), value);
    }
    
    // Probably needs to be TokenAmounts Struct
    // To help with math computations without overflow
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

    public struct Reserves
    {
        // Maybe needs to be TokenAmounts struct
        public ulong ReserveA;
        // Maybe needs to be TokenAmounts struct
        public ulong ReserveB;
        // Preferably LastBlockTimestamp - UNIX timestamp
        public ulong LastBlock;
    }

    public Reserves GetReserves()
    {
        return new Reserves
        {
            ReserveA = ReserveA,
            ReserveB = ReserveB,
            LastBlock = 0
        };
    }

    // Mints tokens when providing liquidity
    // Mints fee LP tokens for Opdex
    public void Mint()
    {
        var reserves = GetReserves();
        var balance0 = Call(TokenA, 0, "GetBalance", new object[] {Address});
        var balance1 = Call(TokenB, 0, "GetBalance", new object[] {Address});
        var amount0 = SafeMath.Sub((ulong)balance0.ReturnValue, reserves.ReserveA);
        var amount1 = SafeMath.Sub((ulong)balance1.ReturnValue, reserves.ReserveB);
        
        // Todo: The hard stuff
    }

    // Burns tokens when removing liquidity
    public void Burn()
    {
        var reserves = GetReserves();
        var tokenA = TokenA; // Gas Savings
        var tokenB = TokenB; // Gas Savings
        var balance0 = Call(tokenA, 0, "GetBalance", new object[] {Address});
        var balance1 = Call(tokenB, 0, "GetBalance", new object[] {Address});
        
        // Once this contract inherits or has token based properties, this will work
        // var liquidity = BalanceOf(Address);
        
        
        // Todo: The hard stuff

    }

    // Swaps tokenA for tokenB or tokenB for tokenA
    public void Swap()
    {
        
    }
}
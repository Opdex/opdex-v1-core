using Stratis.SmartContracts;

public class OpdexV1Factory : SmartContract
{
    public OpdexV1Factory(ISmartContractState smartContractState, Address feeToSetter, Address feeTo) : base(smartContractState)
    {
        FeeToSetter = feeToSetter;
        FeeTo = feeTo;
    }

    private void SetPair(Address tokenA, Address tokenB, Address pair)
    {
        PersistentState.SetAddress($"Pair:{tokenA}:{tokenB}", pair);
    }

    public Address GetPair(Address tokenA, Address tokenB)
    {
        var pair = PersistentState.GetAddress($"Pair:{tokenA}:{tokenB}");

        if (pair == Address.Zero)
        {
            pair = PersistentState.GetAddress($"Pair:{tokenB}:{tokenA}");
        }

        return pair;
    }

    public Address FeeToSetter
    {
        get => PersistentState.GetAddress(nameof(FeeToSetter));
        private set => PersistentState.SetAddress(nameof(FeeToSetter), value);
    }
    
    public Address FeeTo
    {
        get => PersistentState.GetAddress(nameof(FeeTo));
        private set => PersistentState.SetAddress(nameof(FeeTo), value);
    }

    public Address CreatePair(Address tokenA, Address tokenB)
    {
        Assert(tokenA != tokenB, "OpdexV2: IDENTICAL_TOKENS");
        Assert(tokenA != Address.Zero, "OpdexV2: ZERO_ADDRESS");
        Assert(tokenB != Address.Zero, "OpdexV2: ZERO_ADDRESS");

        var pair = GetPair(tokenA, tokenB);

        Assert(pair == Address.Zero, "OpdexV2: PAIR_EXISTS");
        
        var pairContract = Create<OpdexV1Pair>();
        
        pair = pairContract.NewContractAddress;
        
        SetPair(tokenA, tokenB, pair);
        
        // Track all pairs?

        Log(new PairCreatedEvent
        {
            TokenA = tokenA,
            TokenB = tokenB,
            Pair = pair
        });
        
        return pair;
    }

    public void SetFeeTo(Address feeTo)
    {
        Assert(Message.Sender == FeeToSetter, "OpdexV2: FORBIDDEN");
        FeeTo = feeTo;
    }

    public void SetFeeToSetter(Address feeToSetter)
    {
        Assert(Message.Sender == FeeToSetter, "OpdexV2: FORBIDDEN");
        FeeToSetter = feeToSetter;
    }

    public struct PairCreatedEvent
    {
        public Address TokenA { get; set; }
        public Address TokenB { get; set; }
        public Address Pair { get; set; }
    }
}
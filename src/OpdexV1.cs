using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

[Deploy]
public class OpdexV1Controller : SmartContract
{
    public OpdexV1Controller(ISmartContractState smartContractState, Address feeToSetter, Address feeTo) : base(smartContractState)
    {
        FeeToSetter = feeToSetter;
        FeeTo = feeTo;
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

    public void SetFeeTo(Address feeTo)
    {
        Assert(Message.Sender == FeeToSetter, "OpdexV1: FORBIDDEN");
        FeeTo = feeTo;
    }

    public void SetFeeToSetter(Address feeToSetter)
    {
        Assert(Message.Sender == FeeToSetter, "OpdexV1: FORBIDDEN");
        FeeToSetter = feeToSetter;
    }
    
    public Address GetPair(Address token) => PersistentState.GetAddress($"Pair:{token}");
    
    private void SetPair(Address token, Address contract) => PersistentState.SetAddress($"Pair:{token}", contract);
    
    public Address CreatePair(Address token)
    {
        Assert(token != Address.Zero && PersistentState.IsContract(token), "OpdexV1: ZERO_ADDRESS");
        var pair = GetPair(token);
        Assert(pair == Address.Zero, "OpdexV1: PAIR_EXISTS");
        var pairContract = Create<OpdexV1Pair>(0, new object[] {token});
        pair = pairContract.NewContractAddress;
        SetPair(token, pair);
        Log(new PairCreatedEvent { Token = token, Pair = pair });
        return pair;
    }
    
    public AddLiquidityResponseModel AddLiquidity(Address token, ulong amountTokenDesired, ulong amountCrsMin, ulong amountTokenMin, Address to, ulong deadline)
    { 
        var liquidityDto = CalculateLiquidityAmounts(token, Message.Value, amountTokenDesired, amountCrsMin, amountTokenMin);
        SafeTransferFrom(token, Message.Sender, liquidityDto.Pair, liquidityDto.AmountToken);
        var change = Message.Value - liquidityDto.AmountCrs;
        SafeTransfer(liquidityDto.Pair, liquidityDto.AmountCrs);
        var liquidityResponse = Call(liquidityDto.Pair, 0, "Mint", new object[] {to});
        Assert(liquidityResponse.Success, "OpdexV1: INVALID_MINT");
        SafeTransfer(Message.Sender, change);
        return new AddLiquidityResponseModel { AmountCrs = liquidityDto.AmountCrs, 
            AmountToken = liquidityDto.AmountToken, Liquidity = (ulong)liquidityResponse.ReturnValue };
    }
    
    public RemoveLiquidityResponseModel RemoveLiquidity(Address token, ulong liquidity, ulong amountCrsMin, ulong amountTokenMin, Address to, ulong deadline)
    {
        var pair = GetValidatedPair(token);
        SafeTransferFrom(pair, Message.Sender, pair, liquidity);
        var burnDtoResponse = Call(pair, 0, "Burn", new object[] {to});
        var burnResponse = (ulong[])burnDtoResponse.ReturnValue;
        var receivedCrs = burnResponse[0];
        var receivedTokens = burnResponse[1];
        Assert(receivedCrs >= amountCrsMin, "OpdexV1: INSUFFICIENT_CRS_AMOUNT");
        Assert(receivedTokens >= amountTokenMin, "OpdexV1: INSUFFICIENT_TOKEN_AMOUNT");
        return new RemoveLiquidityResponseModel { AmountCrs = receivedCrs, AmountToken = receivedTokens };
    }
    
    public void SwapExactCRSForTokens(ulong amountTokenOutMin, Address token, Address to, ulong deadline)
    {
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountOut = GetAmountOut(Message.Value, reserves[0], reserves[1]);
        Assert(amountOut >= amountTokenOutMin, "OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");
        SafeTransfer(pair, Message.Value);
        Swap(0, amountOut, pair, to);
    }
    
    public void SwapTokensForExactCRS(ulong amountCrsOut, ulong amountTokenInMax, Address token, Address to, ulong deadline)
    {
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountIn = GetAmountIn(amountCrsOut, reserves[1], reserves[0]);
        Assert(amountIn <= amountTokenInMax, "OpdexV1: EXCESSIVE_INPUT_AMOUNT");
        SafeTransferFrom(token, Message.Sender, pair, amountIn);
        Swap(amountCrsOut, 0, pair, to);
    }
    
    public void SwapExactTokensForCRS(ulong amountTokenIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline)
    {
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountOut = GetAmountOut(amountTokenIn, reserves[1], reserves[0]);
        Assert(amountOut >= amountCrsOutMin, "OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");  
        SafeTransferFrom(token, Message.Sender, pair, amountTokenIn);
        Swap(amountOut, 0, pair, to);
    }
    
    public void SwapCRSForExactTokens(ulong amountTokenOut, Address token, Address to, ulong deadline)
    {
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountIn = GetAmountIn(amountTokenOut, reserves[0], reserves[1]);
        Assert(amountIn <= Message.Value, "OpdexV1: EXCESSIVE_INPUT_AMOUNT");
        var change = Message.Value - amountIn;
        SafeTransfer(pair, amountIn);
        Swap(0, amountTokenOut, pair, to);
        SafeTransfer(Message.Sender, change);
    }
    
    public ulong GetLiquidityQuote(ulong amountA, ulong reserveA, ulong reserveB)
    {
        Assert(amountA > 0, "OpdexV1: INSUFFICIENT_AMOUNT");
        Assert(reserveA > 0 && reserveB > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        return checked(amountA * reserveB) / reserveA;
    }
    
    public ulong GetAmountOut(ulong amountIn, ulong reserveIn, ulong reserveOut)
    {
        Assert(amountIn > 0, "OpdexV1: INSUFFICIENT_INPUT_AMOUNT");
        Assert(reserveIn > 0 && reserveOut > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        var amountInWithFee = checked(amountIn * 997);
        var numerator = checked(amountInWithFee * reserveOut);
        var denominator = checked(checked(reserveIn * 1000) + amountInWithFee);
        return numerator / denominator;
    }

    public ulong GetAmountIn(ulong amountOut, ulong reserveIn, ulong reserveOut)
    {
        Assert(amountOut > 0, "OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");
        Assert(reserveIn > 0 && reserveOut > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        var numerator = checked(checked(reserveIn * amountOut) * 1000);
        var denominator = checked(checked(reserveOut - amountOut) * 997);
        return checked((numerator / denominator) + 1);
    }
    
    // Todo: Preferably split this method to allow for a public method to calculate this for free via local call
    private CalcLiquidityModel CalculateLiquidityAmounts(Address token, ulong amountCrsDesired, ulong amountTokenDesired, ulong amountCrsMin, ulong amountTokenMin)
    {
        ulong reserveCrs = 0;
        ulong reserveToken = 0;
        var pair = GetPair(token);
        if (pair == Address.Zero) pair = CreatePair(token);
        else
        {
            var reserves = GetReserves(pair);
            reserveCrs = reserves[0];
            reserveToken = reserves[1];
        }
        ulong amountCrs;
        ulong amountToken;
        if (reserveCrs == 0 && reserveToken == 0)
        {
            amountCrs = amountCrsDesired;
            amountToken = amountTokenDesired;
        }
        else
        {
            var amountTokenOptimal = GetLiquidityQuote(amountCrsDesired, reserveCrs, reserveToken);
            if (amountTokenOptimal <= amountTokenDesired)
            {
                Assert(amountTokenOptimal >= amountTokenMin, "OpdexV1: INSUFFICIENT_B_AMOUNT");
                amountCrs = amountCrsDesired;
                amountToken = amountTokenOptimal;
            }
            else
            {
                var amountCrsOptimal = GetLiquidityQuote(amountTokenDesired, reserveToken, reserveCrs);
                Assert(amountCrsOptimal <= amountCrsDesired && amountCrsOptimal >= amountCrsMin, "OpdexV1: INSUFFICIENT_A_AMOUNT");
                amountCrs = amountCrsOptimal;
                amountToken = amountTokenDesired;
            }
        }
        return new CalcLiquidityModel { AmountCrs = amountCrs, AmountToken = amountToken, Pair = pair };
    }
    
    private void Swap(ulong amountCrsOut, ulong amountTokenOut, Address pair, Address to)
    {
        var response = Call(pair, 0, "Swap", new object[] {amountCrsOut, amountTokenOut, to});
        Assert(response.Success, "OpdexV1: INVALID_SWAP_ATTEMPT");
    }
    
    private void SafeTransfer(Address to, ulong amount)
    {
        if (amount == 0) return; 
        var result = Transfer(to, amount);
        Assert(result.Success, "OpdexV1: INVALID_TRANSFER");
    }
    
    private void SafeTransferFrom(Address token, Address from, Address to, ulong amount)
    {
        var result = Call(token, 0, "TransferFrom", new object[] {from, to, amount});
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_FROM");
    }
    
    private ulong[] GetReserves(Address pair)
    {
        var reservesResponse = Call(pair, 0, "GetReserves");
        Assert(reservesResponse.Success, "OpdexV1: INVALID_PAIR");
        return (ulong[])reservesResponse.ReturnValue;
    }
    
    private Address GetValidatedPair(Address token)
    {
        var pair = GetPair(token);
        Assert(pair != Address.Zero, "OpdexV1: INVALID_PAIR");
        return pair;
    }

    public struct AddLiquidityResponseModel
    {
        public ulong AmountCrs;
        public ulong AmountToken;
        public ulong Liquidity;
    }

    public struct RemoveLiquidityResponseModel
    {
        public ulong AmountCrs;
        public ulong AmountToken;
    }
    
    private struct CalcLiquidityModel
    {
        public ulong AmountCrs;
        public ulong AmountToken;
        public Address Pair;
    }

    public struct PairCreatedEvent
    {
        public Address Token;
        public Address Pair;
    }
}

public class OpdexV1Pair : SmartContract, IStandardToken
{
    private const ulong MinimumLiquidity = 1000;
    public OpdexV1Pair(ISmartContractState smartContractState, Address token) : base(smartContractState)
    {
        Controller = Message.Sender;
        Token = token;
    }

    public override void Receive() => base.Receive();

    public Address Controller
    {
        get => PersistentState.GetAddress(nameof(Controller));
        private set => PersistentState.SetAddress(nameof(Controller), value);
    }
    
    public Address Token
    {
        get => PersistentState.GetAddress(nameof(Token));
        private set => PersistentState.SetAddress(nameof(Token), value);
    }
    
    public ulong ReserveCrs
    {
        get => PersistentState.GetUInt64(nameof(ReserveCrs));
        private set => PersistentState.SetUInt64(nameof(ReserveCrs), value);
    }
    
    public ulong ReserveToken
    {
        get => PersistentState.GetUInt64(nameof(ReserveToken));
        private set => PersistentState.SetUInt64(nameof(ReserveToken), value);
    }
    
    public ulong KLast
    {
        get => PersistentState.GetUInt64(nameof(KLast));
        private set => PersistentState.SetUInt64(nameof(KLast), value);
    }

    public ulong TotalSupply 
    {
        get => PersistentState.GetUInt64(nameof(TotalSupply));
        private set => PersistentState.SetUInt64(nameof(TotalSupply), value);
    }

    public uint Decimals => 8;
    
    public string Name => "Opdex Liquidity Pool Token";
    
    public string Symbol => "OLPT";
    
    public ulong GetBalance(Address address) => PersistentState.GetUInt64($"Balance:{address}");
    
    private void SetBalance(Address address, ulong amount) => PersistentState.SetUInt64($"Balance:{address}", amount);

    // Added for IStandardToken interface compatibility
    public ulong Allowance(Address owner, Address spender) => GetAllowance(owner, spender);
    
    public ulong GetAllowance(Address owner, Address spender) => PersistentState.GetUInt64($"Allowance:{owner}:{spender}");
    
    private void SetAllowance(Address owner, Address spender, ulong amount) => PersistentState.SetUInt64($"Allowance:{owner}:{spender}", amount);
    
    public bool TransferTo(Address to, ulong amount) => TransferExecute(Message.Sender, to, amount);
    
    public bool TransferFrom(Address from, Address to, ulong amount)
    {
        var allowance = GetAllowance(from, Message.Sender);
        if (allowance > 0) SetAllowance(from, Message.Sender, checked(allowance - amount));
        return TransferExecute(from, to, amount);
    }

    // Added for IStandardToken interface compatibility, currentAmount goes unused
    public bool Approve(Address spender, ulong currentAmount, ulong amount) => Approve(spender, amount);
    
    public bool Approve(Address spender, ulong amount)
    {
        SetAllowance(Message.Sender, spender, amount);
        Log(new ApprovalEvent {Owner = Message.Sender, Spender = spender, Amount = amount, EventType = nameof(ApprovalEvent)});
        return true;
    }

    public ulong Mint(Address to)
    {
        var reserves = GetReserves();
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(Token, Address);
        var amountCrs = checked(balanceCrs - reserves[0]);
        var amountToken = checked(balanceToken - reserves[1]);
        var totalSupply = TotalSupply;
        ulong liquidity;
        if (totalSupply == 0)
        {
            liquidity = checked(Sqrt(checked(amountCrs * amountToken)) - MinimumLiquidity);
            MintExecute(Address.Zero, MinimumLiquidity);
        }
        else
        {
            var amountCrsLiquidity = checked(amountCrs * totalSupply) / reserves[0];
            var amountTokenLiquidity = checked(amountToken * totalSupply) / reserves[1];
            liquidity = amountCrsLiquidity > amountTokenLiquidity ? amountTokenLiquidity : amountCrsLiquidity;
        }
        Assert(liquidity > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        MintFee(reserves[0], reserves[1]);
        MintExecute(to, liquidity);
        Update(balanceCrs, balanceToken);
        Log(new MintEvent { AmountCrs = amountCrs, AmountToken = amountToken, Sender = Message.Sender, EventType = nameof(MintEvent) });
        return liquidity;
    }

    public ulong[] Burn(Address to)
    {
        var reserves = GetReserves();
        var address = Address;
        var token = Token;
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(token, address);
        var liquidity = GetBalance(address);
        var totalSupply = TotalSupply;
        var amountCrs = checked(liquidity * balanceCrs) / totalSupply;
        var amountToken = checked(liquidity * balanceToken) / totalSupply;
        Assert(amountCrs > 0 && amountToken > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY_BURNED");
        MintFee(reserves[0], reserves[1]);
        BurnExecute(address, liquidity);
        SafeTransfer(to, amountCrs);
        SafeTransferTo(token, to, amountToken);
        balanceCrs = Balance;
        balanceToken = GetSrcBalance(token, address);
        Update(balanceCrs, balanceToken);
        KLast = checked(checked(reserves[0] - balanceCrs) * checked(reserves[1] - balanceToken));
        Log(new BurnEvent { Sender = Message.Sender, To = to, AmountCrs = amountCrs, AmountToken = amountToken, EventType = nameof(BurnEvent) });
        return new [] {amountCrs, amountToken};
    }

    public void Swap(ulong amountCrsOut, ulong amountTokenOut, Address to)
    {
        Assert(amountCrsOut > 0 ^ amountTokenOut > 0, "OpdexV1: INVALID_OUTPUT_AMOUNT");
        var reserves = GetReserves();
        Assert(amountCrsOut < reserves[0] && amountTokenOut < reserves[1], "OpdexV1: INSUFFICIENT_LIQUIDITY");
        var token = Token;
        Assert(to != token, "OpdexV1: INVALID_TO");
        if (amountCrsOut > 0) SafeTransfer(to, amountCrsOut);
        if (amountTokenOut > 0) SafeTransferTo(token, to, amountTokenOut);
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(token, Address);
        var crsDifference = checked(reserves[0] - amountCrsOut);
        var amountCrsIn = balanceCrs > crsDifference ? checked(balanceCrs - crsDifference) : 0;
        var srcDifference = checked(reserves[1] - amountTokenOut);
        var amountTokenIn = balanceToken > srcDifference ? checked(balanceToken - srcDifference) : 0;
        Assert(amountCrsIn > 0 || amountTokenIn > 0, "OpdexV1: INSUFFICIENT_INPUT_AMOUNT");
        var balanceCrsAdjusted = checked(checked(balanceCrs * 1_000) - checked(amountCrsIn * 3));
        var balanceTokenAdjusted = checked(checked(balanceToken * 1_000) - checked(amountTokenIn * 3));
        Assert(checked(balanceCrsAdjusted * balanceTokenAdjusted) >= checked(checked(reserves[0] * reserves[1]) * 1_000_000)); // 1_000 * 1_000
        Update(balanceCrs, balanceToken);
        KLast = checked(ReserveCrs * ReserveToken);
        Log(new SwapEvent { AmountCrsIn = amountCrsIn, AmountCrsOut = amountCrsOut, AmountTokenIn = amountTokenIn,
             AmountTokenOut = amountTokenOut, Sender = Message.Sender, To = to, EventType = nameof(SwapEvent) });
    }

    public void Skim(Address to)
    {
        var token = Token;
        var balanceToken = checked(GetSrcBalance(token, Address) - ReserveToken);
        var balanceCrs = checked(Balance - ReserveCrs);
        SafeTransfer(to, balanceToken);
        SafeTransferTo(token, to, balanceCrs);
        // Todo: Should this log an event?
    }  

    public void Sync() => Update(Balance, GetSrcBalance(Token, Address));

    public ulong[] GetReserves() => new [] { ReserveCrs, ReserveToken };
    
    private void Update(ulong balanceCrs, ulong balanceToken)
    {
        ReserveCrs = balanceCrs;
        ReserveToken = balanceToken;
        Log(new SyncEvent { ReserveCrs = balanceCrs, ReserveToken = balanceToken, EventType = nameof(SyncEvent) });
    }
    
    private void MintFee(ulong reserveCrs, ulong reserveToken)
    {
        var kLast = KLast;
        if (kLast == 0) return;
        var rootK = Sqrt(checked(reserveCrs * reserveToken));
        var rootKLast = Sqrt(kLast);
        if (rootK <= rootKLast) return;
        var numerator = checked(TotalSupply * checked(rootK - rootKLast));
        var denominator = checked(checked(rootK * 5) + rootKLast);
        var liquidity = numerator / denominator;
        if (liquidity == 0) return;
        var feeToResponse = Call(Controller, 0, "get_FeeTo");
        var feeTo = (Address)feeToResponse.ReturnValue;
        Assert(feeToResponse.Success && feeTo != Address.Zero, "OpdexV1: INVALID_FEE_TO_ADDRESS");
        MintExecute(feeTo, liquidity);
    }
    
    private void MintExecute(Address to, ulong amount)
    {
        TotalSupply = checked(TotalSupply + amount);
        SetBalance(to, checked(GetBalance(to) + amount));
        Log(new TransferEvent { From = Address.Zero, To = to, Amount = amount, EventType = nameof(TransferEvent) });
    }
    
    private ulong GetSrcBalance(Address token, Address owner)
    {
        var balanceResponse = Call(token, 0, "GetBalance", new object[] {owner});
        Assert(balanceResponse.Success, "OpdexV1: INVALID_BALANCE");
        return (ulong)balanceResponse.ReturnValue;
    }
    
    private void BurnExecute(Address from, ulong amount)
    {
        SetBalance(from, checked(GetBalance(from) - amount));
        TotalSupply = checked(TotalSupply - amount);
        Log(new TransferEvent { From = from, To = Address.Zero, Amount = amount, EventType = nameof(TransferEvent) });
    }
    
    private bool TransferExecute(Address from, Address to, ulong amount)
    {
        SetBalance(from, checked(GetBalance(from) - amount));
        SetBalance(to, checked(GetBalance(to) + amount));
        Log(new TransferEvent {From = from, To = to, Amount = amount, EventType = nameof(TransferEvent)});
        return true;
    }
    
    private void SafeTransfer(Address to, ulong amount)
    {
        if (amount == 0) return;
        var result = Transfer(to, amount);
        Assert(result.Success, "OpdexV1: INVALID_TRANSFER");
    }
    
    private void SafeTransferTo(Address token, Address to, ulong amount)
    {
        if (amount == 0) return;
        var result = Call(token, 0, "TransferTo", new object[] {to, amount});
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_FROM");
    }
    
    private static ulong Sqrt(ulong value)
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

    public struct SyncEvent
    {
        public ulong ReserveCrs;
        public ulong ReserveToken;
        public string EventType;
    }
    
    public struct MintEvent
    {
        [Index] public Address Sender;
        public ulong AmountCrs;
        public ulong AmountToken;
        public string EventType;
    }
    
    public struct BurnEvent
    {
        [Index] public Address Sender;
        [Index] public Address To;
        public ulong AmountCrs;
        public ulong AmountToken;
        public string EventType;
    }
    
    public struct SwapEvent
    {
        [Index] public Address Sender;
        [Index] public Address To;
        public ulong AmountCrsIn;
        public ulong AmountTokenIn;
        public ulong AmountCrsOut;
        public ulong AmountTokenOut;
        public string EventType;
    }
    
    public struct ApprovalEvent
    {
        [Index] public Address Owner;
        [Index] public Address Spender;
        public ulong Amount;
        public string EventType;
    }

    public struct TransferEvent
    {
        [Index] public Address From;
        [Index] public Address To;
        public ulong Amount;        
        public string EventType;
    }
}
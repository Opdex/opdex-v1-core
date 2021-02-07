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
        get => State.GetAddress(nameof(FeeToSetter));
        private set => State.SetAddress(nameof(FeeToSetter), value);
    }
    
    public Address FeeTo
    {
        get => State.GetAddress(nameof(FeeTo));
        private set => State.SetAddress(nameof(FeeTo), value);
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
    
    public Address GetPair(Address token) => State.GetAddress($"Pair:{token}");
    
    private void SetPair(Address token, Address contract) => State.SetAddress($"Pair:{token}", contract);
    
    public Address CreatePair(Address token)
    {
        Assert(token != Address.Zero && State.IsContract(token), "OpdexV1: ZERO_ADDRESS");
        var pair = GetPair(token);
        Assert(pair == Address.Zero, "OpdexV1: PAIR_EXISTS");
        var pairContract = Create<OpdexV1Pair>(0, new object[] {token});
        pair = pairContract.NewContractAddress;
        SetPair(token, pair);
        Log(new PairCreatedEvent { Token = token, Pair = pair, EventType = nameof(PairCreatedEvent) });
        return pair;
    }
    
    public AddLiquidityResponseModel AddLiquidity(Address token, UInt256 amountTokenDesired, ulong amountCrsMin, UInt256 amountTokenMin, Address to, ulong deadline)
    { 
        Assert(deadline == 0 || Block.Number <= deadline, "OpdexV1: Deadline passed, trade expired");
        var liquidityDto = CalculateLiquidityAmounts(token, Message.Value, amountTokenDesired, amountCrsMin, amountTokenMin);
        SafeTransferFrom(token, Message.Sender, liquidityDto.Pair, liquidityDto.AmountToken);
        var change = Message.Value - liquidityDto.AmountCrs;
        SafeTransfer(liquidityDto.Pair, liquidityDto.AmountCrs);
        var liquidityResponse = Call(liquidityDto.Pair, 0, "Mint", new object[] {to});
        Assert(liquidityResponse.Success, "OpdexV1: INVALID_MINT_RESPONSE");
        SafeTransfer(Message.Sender, change);
        return new AddLiquidityResponseModel { AmountCrs = liquidityDto.AmountCrs, 
            AmountToken = liquidityDto.AmountToken, Liquidity = (UInt256)liquidityResponse.ReturnValue };
    }
    
    public RemoveLiquidityResponseModel RemoveLiquidity(Address token, UInt256 liquidity, ulong amountCrsMin, UInt256 amountTokenMin, Address to, ulong deadline)
    {
        Assert(deadline == 0 || Block.Number <= deadline, "OpdexV1: Deadline passed, trade expired");
        var pair = GetValidatedPair(token);
        SafeTransferFrom(pair, Message.Sender, pair, liquidity);
        var burnDtoResponse = Call(pair, 0, "Burn", new object[] {to});
        var burnResponse = (UInt256[])burnDtoResponse.ReturnValue;
        var receivedCrs = (ulong)burnResponse[0];
        var receivedTokens = burnResponse[1];
        Assert(receivedCrs >= amountCrsMin, "OpdexV1: INSUFFICIENT_CRS_AMOUNT");
        Assert(receivedTokens >= amountTokenMin, "OpdexV1: INSUFFICIENT_TOKEN_AMOUNT");
        return new RemoveLiquidityResponseModel { AmountCrs = receivedCrs, AmountToken = receivedTokens };
    }

    public void Stake(Address token, UInt256 amount)
    {
        var OPDT = Address.Zero;
        Assert(token != OPDT, "OpdexV1: Cannot stake OPDT.");
        var pair = GetValidatedPair(token);
        SafeTransferFrom(OPDT, Message.Sender, pair, amount);
        var response = Call(pair, 0, "Stake", new object[] {Message.Sender});
        Assert(response.Success);
    }
    
    public void SwapExactCRSForTokens(UInt256 amountTokenOutMin, Address token, Address to, ulong deadline)
    {
        Assert(deadline == 0 || Block.Number <= deadline, "OpdexV1: Deadline passed, trade expired");
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountOut = GetAmountOut(Message.Value, reserves[0], reserves[1]);
        Assert(amountOut >= amountTokenOutMin, "OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");
        SafeTransfer(pair, Message.Value);
        Swap(0, amountOut, pair, to);
    }
    
    public void SwapTokensForExactCRS(ulong amountCrsOut, UInt256 amountTokenInMax, Address token, Address to, ulong deadline)
    {
        Assert(deadline == 0 || Block.Number <= deadline, "OpdexV1: Deadline passed, trade expired");
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountIn = GetAmountIn(amountCrsOut, reserves[1], reserves[0]);
        Assert(amountIn <= amountTokenInMax, "OpdexV1: EXCESSIVE_INPUT_AMOUNT");
        SafeTransferFrom(token, Message.Sender, pair, amountIn);
        Swap(amountCrsOut, 0, pair, to);
    }
    
    public void SwapExactTokensForCRS(UInt256 amountTokenIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline)
    {
        Assert(deadline == 0 || Block.Number <= deadline, "OpdexV1: Deadline passed, trade expired");
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountOut = GetAmountOut(amountTokenIn, reserves[1], reserves[0]);
        Assert(amountOut >= amountCrsOutMin, "OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");  
        SafeTransferFrom(token, Message.Sender, pair, amountTokenIn);
        Swap((ulong)amountOut, 0, pair, to);
    }
    
    public void SwapCRSForExactTokens(UInt256 amountTokenOut, Address token, Address to, ulong deadline)
    {
        Assert(deadline == 0 || Block.Number <= deadline, "OpdexV1: Deadline passed, trade expired");
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountIn = (ulong)GetAmountIn(amountTokenOut, reserves[0], reserves[1]);
        Assert(amountIn <= Message.Value, "OpdexV1: EXCESSIVE_INPUT_AMOUNT");
        var change = Message.Value - amountIn;
        SafeTransfer(pair, amountIn);
        Swap(0, amountTokenOut, pair, to);
        SafeTransfer(Message.Sender, change);
    }
    
    public UInt256 GetLiquidityQuote(UInt256 amountA, UInt256 reserveA, UInt256 reserveB)
    {
        Assert(amountA > 0, "OpdexV1: INSUFFICIENT_AMOUNT");
        Assert(reserveA > 0 && reserveB > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        return amountA * reserveB / reserveA;
    }
    
    public UInt256 GetAmountOut(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut)
    {
        Assert(amountIn > 0, "OpdexV1: INSUFFICIENT_INPUT_AMOUNT");
        Assert(reserveIn > 0 && reserveOut > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        var amountInWithFee = amountIn * 997;
        var numerator = amountInWithFee * reserveOut;
        var denominator = reserveIn * 1000 + amountInWithFee;
        return numerator / denominator;
    }

    public UInt256 GetAmountIn(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut)
    {
        Assert(amountOut > 0, "OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");
        Assert(reserveIn > 0 && reserveOut > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        var numerator = reserveIn * amountOut * 1000;
        var denominator = (reserveOut - amountOut) * 997;
        return numerator / denominator + 1;
    }
    
    // Todo: Preferably split this method to allow for a public method to calculate this for free via local call
    private CalcLiquidityModel CalculateLiquidityAmounts(Address token, ulong amountCrsDesired, UInt256 amountTokenDesired, ulong amountCrsMin, UInt256 amountTokenMin)
    {
        UInt256 reserveCrs = 0;
        UInt256 reserveToken = 0;
        var pair = GetPair(token);
        if (pair == Address.Zero) pair = CreatePair(token);
        else
        {
            var reserves = GetReserves(pair);
            reserveCrs = reserves[0];
            reserveToken = reserves[1];
        }
        UInt256 amountCrs;
        UInt256 amountToken;
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
        return new CalcLiquidityModel { AmountCrs = (ulong)amountCrs, AmountToken = amountToken, Pair = pair };
    }
    
    private void Swap(ulong amountCrsOut, UInt256 amountTokenOut, Address pair, Address to)
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
    
    private void SafeTransferFrom(Address token, Address from, Address to, UInt256 amount)
    {
        var result = Call(token, 0, "TransferFrom", new object[] {from, to, amount});
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_FROM");
    }
    
    private UInt256[] GetReserves(Address pair)
    {
        var reservesResponse = Call(pair, 0, "GetReserves");
        Assert(reservesResponse.Success, "OpdexV1: INVALID_PAIR");
        return (UInt256[])reservesResponse.ReturnValue;
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
        public UInt256 AmountToken;
        public UInt256 Liquidity;
    }

    public struct RemoveLiquidityResponseModel
    {
        public ulong AmountCrs;
        public UInt256 AmountToken;
    }
    
    private struct CalcLiquidityModel
    {
        public ulong AmountCrs;
        public UInt256 AmountToken;
        public Address Pair;
    }

    public struct PairCreatedEvent
    {
        public Address Token;
        public Address Pair;
        public string EventType;
    }
}

public class OpdexV1Pair : SmartContract, IStandardToken256
{
    private const ulong MinimumLiquidity = 1000;
    public OpdexV1Pair(ISmartContractState smartContractState, Address token) : base(smartContractState)
    {
        Controller = Message.Sender;
        Token = token;
    }

    public Address Controller
    {
        get => State.GetAddress(nameof(Controller));
        private set => State.SetAddress(nameof(Controller), value);
    }
    
    public Address Token
    {
        get => State.GetAddress(nameof(Token));
        private set => State.SetAddress(nameof(Token), value);
    }
    
    public ulong ReserveCrs
    {
        get => State.GetUInt64(nameof(ReserveCrs));
        private set => State.SetUInt64(nameof(ReserveCrs), value);
    }
    
    public UInt256 ReserveToken
    {
        get => State.GetUInt256(nameof(ReserveToken));
        private set => State.SetUInt256(nameof(ReserveToken), value);
    }
    
    public UInt256 KLast
    {
        get => State.GetUInt256(nameof(KLast));
        private set => State.SetUInt256(nameof(KLast), value);
    }

    public UInt256 TotalSupply 
    {
        get => State.GetUInt256(nameof(TotalSupply));
        private set => State.SetUInt256(nameof(TotalSupply), value);
    }

    public byte Decimals => 8;
    
    public string Name => "Opdex Liquidity Pool Token";
    
    public string Symbol => "OLPT";
    
    public UInt256 GetBalance(Address address) => State.GetUInt256($"Balance:{address}");
    
    private void SetBalance(Address address, UInt256 amount) => State.SetUInt256($"Balance:{address}", amount);

    // Added for IStandardToken interface compatibility
    public UInt256 Allowance(Address owner, Address spender) => GetAllowance(owner, spender);
    
    public UInt256 GetAllowance(Address owner, Address spender) => State.GetUInt256($"Allowance:{owner}:{spender}");
    
    private void SetAllowance(Address owner, Address spender, UInt256 amount) => State.SetUInt256($"Allowance:{owner}:{spender}", amount);
    
    public bool TransferTo(Address to, UInt256 amount) => TransferExecute(Message.Sender, to, amount);
    
    public bool TransferFrom(Address from, Address to, UInt256 amount)
    {
        var allowance = GetAllowance(from, Message.Sender);
        if (allowance > 0) SetAllowance(from, Message.Sender, allowance - amount);
        return TransferExecute(from, to, amount);
    }

    // Added for IStandardToken interface compatibility, currentAmount goes unused
    public bool Approve(Address spender, UInt256 currentAmount, UInt256 amount) => Approve(spender, amount);
    
    public bool Approve(Address spender, UInt256 amount)
    {
        SetAllowance(Message.Sender, spender, amount);
        Log(new ApprovalEvent {Owner = Message.Sender, Spender = spender, Amount = amount, EventType = nameof(ApprovalEvent)});
        return true;
    }

    public UInt256 Mint(Address to)
    {
        var reserves = GetReserves();
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(Token, Address);
        var amountCrs = (ulong)(balanceCrs - reserves[0]);
        var amountToken = balanceToken - reserves[1];
        var totalSupply = TotalSupply;
        MintFee((ulong)reserves[0], reserves[1]);
        UInt256 liquidity;
        if (totalSupply == 0)
        {
            liquidity = Sqrt(amountCrs * amountToken) - MinimumLiquidity;
            MintExecute(Address.Zero, MinimumLiquidity);
        }
        else
        {
            var amountCrsLiquidity = (amountCrs * totalSupply) / reserves[0];
            var amountTokenLiquidity = (amountToken * totalSupply) / reserves[1];
            liquidity = amountCrsLiquidity > amountTokenLiquidity ? amountTokenLiquidity : amountCrsLiquidity;
        }
        Assert(liquidity > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        MintExecute(to, liquidity);
        Update(balanceCrs, balanceToken);
        KLast = ReserveCrs * ReserveToken;
        Log(new MintEvent { AmountCrs = amountCrs, AmountToken = amountToken, Sender = Message.Sender, EventType = nameof(MintEvent) });
        return liquidity;
    }

    public UInt256[] Burn(Address to)
    {
        var reserves = GetReserves();
        var address = Address;
        var token = Token;
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(token, address);
        var liquidity = GetBalance(address);
        var totalSupply = TotalSupply;
        var amountCrs = (ulong)((liquidity * balanceCrs) / totalSupply);
        var amountToken = (liquidity * balanceToken) / totalSupply;
        Assert(amountCrs > 0 && amountToken > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY_BURNED");
        MintFee((ulong)reserves[0], reserves[1]);
        BurnExecute(address, liquidity);
        SafeTransfer(to, amountCrs);
        SafeTransferTo(token, to, amountToken);
        balanceCrs = Balance;
        balanceToken = GetSrcBalance(token, address);
        Update(balanceCrs, balanceToken);
        Log(new BurnEvent { Sender = Message.Sender, To = to, AmountCrs = amountCrs, AmountToken = amountToken, EventType = nameof(BurnEvent) });
        return new [] {amountCrs, amountToken};
    }

    public void Swap(ulong amountCrsOut, UInt256 amountTokenOut, Address to)
    {
        var reserves = GetReserves();
        var token = Token;
        Assert(amountCrsOut > 0 ^ amountTokenOut > 0, "OpdexV1: INVALID_OUTPUT_AMOUNT");
        Assert(amountCrsOut < reserves[0] && amountTokenOut < reserves[1], "OpdexV1: INSUFFICIENT_LIQUIDITY");
        Assert(to != token, "OpdexV1: INVALID_TO");
        SafeTransfer(to, amountCrsOut);
        SafeTransferTo(token, to, amountTokenOut);
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(token, Address);
        var crsDifference = (reserves[0] - amountCrsOut);
        var amountCrsIn = balanceCrs > crsDifference ? (ulong)(balanceCrs - crsDifference) : 0;
        var srcDifference = (reserves[1] - amountTokenOut);
        var amountTokenIn = balanceToken > srcDifference ? balanceToken - srcDifference : 0;
        Assert(amountCrsIn > 0 || amountTokenIn > 0, "OpdexV1: INSUFFICIENT_INPUT_AMOUNT");
        var balanceCrsAdjusted = (balanceCrs * 1_000) - (amountCrsIn * 3);
        var balanceTokenAdjusted = (balanceToken * 1_000) - (amountTokenIn * 3);
        Assert(balanceCrsAdjusted * balanceTokenAdjusted >= (reserves[0] * reserves[1]) * 1_000_000); // 1_000 * 1_000
        Update(balanceCrs, balanceToken);
        KLast = ReserveCrs * ReserveToken;
        Log(new SwapEvent { AmountCrsIn = amountCrsIn, AmountCrsOut = amountCrsOut, AmountTokenIn = amountTokenIn,
             AmountTokenOut = amountTokenOut, Sender = Message.Sender, To = to, EventType = nameof(SwapEvent) });
    }

    public void Borrow(ulong amountCrs, UInt256 amountToken, Address to, string callbackMethod, byte[] data)
    {
        var token = Token;
        Assert(to != Address.Zero && to != Address && to != token);
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(token, Address);
        SafeTransferTo(token, to, amountToken);
        var result = Call(to, amountCrs, callbackMethod, new object[] {data});
        Assert(result.Success);
        Assert(balanceCrs == Balance && balanceToken == GetSrcBalance(token, Address), "OpdexV1: INSUFFICIENT_DEBT_PAID");
    }

    public void Stake(Address to)
    {
        // Check last saved staking weight of contract
        // Use difference of now and last saved to determine this users weight
        // KLast calcs
        // Persist struct, users weight, klast
    }

    public void StopStaking(Address to)
    {
        // Stop staking, return OPDT 
        WithdrawStakingRewards(to);
    }

    public void WithdrawStakingRewards(Address to)
    {
        // Keep staking, withdraw rewards
        // Uses stakers weight along with LP tokens assigned to this pairs Address
    }

    public void Skim(Address to)
    {
        var token = Token;
        var balanceToken = GetSrcBalance(token, Address) - ReserveToken;
        var balanceCrs = Balance - ReserveCrs;
        SafeTransfer(to, balanceCrs);
        SafeTransferTo(token, to, balanceToken);
    }  

    public void Sync() => Update(Balance, GetSrcBalance(Token, Address));

    public UInt256[] GetReserves() => new [] { ReserveCrs, ReserveToken };
    
    private void Update(ulong balanceCrs, UInt256 balanceToken)
    {
        ReserveCrs = balanceCrs;
        ReserveToken = balanceToken;
        Log(new SyncEvent { ReserveCrs = balanceCrs, ReserveToken = balanceToken, EventType = nameof(SyncEvent) });
    }
    
    private void MintFee(ulong reserveCrs, UInt256 reserveToken)
    {
        var kLast = KLast;
        if (kLast == 0) return;
        var rootK = Sqrt(reserveCrs * reserveToken);
        var rootKLast = Sqrt(kLast);
        if (rootK <= rootKLast) return;
        var numerator = TotalSupply * (rootK - rootKLast);
        var denominator = (rootK * 5) + rootKLast;
        var liquidity = numerator / denominator;
        if (liquidity == 0) return;
        var feeToResponse = Call(Controller, 0, "get_FeeTo");
        var feeTo = (Address)feeToResponse.ReturnValue;
        Assert(feeToResponse.Success && feeTo != Address.Zero, "OpdexV1: INVALID_FEE_TO_ADDRESS");
        MintExecute(feeTo, liquidity); // Staking would mint the fee to this pairs Address
    }
    
    private void MintExecute(Address to, UInt256 amount)
    {
        TotalSupply += amount;
        SetBalance(to, GetBalance(to) + amount);
        Log(new TransferEvent { From = Address.Zero, To = to, Amount = amount, EventType = nameof(TransferEvent) });
    }
    
    private UInt256 GetSrcBalance(Address token, Address owner)
    {
        var balanceResponse = Call(token, 0, "GetBalance", new object[] {owner});
        Assert(balanceResponse.Success, "OpdexV1: INVALID_BALANCE");
        return (UInt256)balanceResponse.ReturnValue;
    }
    
    private void BurnExecute(Address from, UInt256 amount)
    {
        SetBalance(from, GetBalance(from) - amount);
        TotalSupply = TotalSupply - amount;
        Log(new TransferEvent { From = from, To = Address.Zero, Amount = amount, EventType = nameof(TransferEvent) });
    }
    
    private bool TransferExecute(Address from, Address to, UInt256 amount)
    {
        SetBalance(from, GetBalance(from) - amount);
        SetBalance(to, GetBalance(to) + amount);
        Log(new TransferEvent {From = from, To = to, Amount = amount, EventType = nameof(TransferEvent)});
        return true;
    }
    
    private void SafeTransfer(Address to, ulong amount)
    {
        if (amount == 0) return;
        var result = Transfer(to, amount);
        Assert(result.Success, "OpdexV1: INVALID_TRANSFER");
    }
    
    private void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        var result = Call(token, 0, "TransferTo", new object[] {to, amount});
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_FROM");
    }
    
    private static UInt256 Sqrt(UInt256 value)
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
        public UInt256 ReserveToken;
        public string EventType;
    }
    
    public struct MintEvent
    {
        [Index] public Address Sender;
        public ulong AmountCrs;
        public UInt256 AmountToken;
        public string EventType;
    }
    
    public struct BurnEvent
    {
        [Index] public Address Sender;
        [Index] public Address To;
        public ulong AmountCrs;
        public UInt256 AmountToken;
        public string EventType;
    }
    
    public struct SwapEvent
    {
        [Index] public Address Sender;
        [Index] public Address To;
        public ulong AmountCrsIn;
        public UInt256 AmountTokenIn;
        public ulong AmountCrsOut;
        public UInt256 AmountTokenOut;
        public string EventType;
    }
    
    public struct ApprovalEvent
    {
        [Index] public Address Owner;
        [Index] public Address Spender;
        public UInt256 Amount;
        public string EventType;
    }

    public struct TransferEvent
    {
        [Index] public Address From;
        [Index] public Address To;
        public UInt256 Amount;        
        public string EventType;
    }
}
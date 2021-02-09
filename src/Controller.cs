using Stratis.SmartContracts;

[Deploy]
public class OpdexV1Controller : ContractBase
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

    public Address GetPair(Address token)
    {
        return State.GetAddress($"Pair:{token}");
    }
    
    private void SetPair(Address token, Address contract)
    {
        State.SetAddress($"Pair:{token}", contract);
    }
    
    public Address CreatePair(Address token)
    {
        Assert(token != Address.Zero && State.IsContract(token), "OpdexV1: ZERO_ADDRESS");
        var pair = GetPair(token);
        Assert(pair == Address.Zero, "OpdexV1: PAIR_EXISTS");
        var pairContract = Create<OpdexV1Pair>(0, new object[] {token});
        pair = pairContract.NewContractAddress;
        SetPair(token, pair);
        Log(new PairCreatedEvent { Token = token, Pair = pair, EventTypeId = (byte)EventType.PairCreatedEvent });
        return pair;
    }
    
    public AddLiquidityResponseModel AddLiquidity(Address token, UInt256 amountTokenDesired, ulong amountCrsMin, UInt256 amountTokenMin, Address to, ulong deadline)
    { 
        ValidateDeadline(deadline);
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
        ValidateDeadline(deadline);
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

    // public void Stake(Address token, UInt256 amount)
    // {
    //     var OPDT = Address.Zero;
    //     Assert(token != OPDT, "OpdexV1: Cannot stake OPDT.");
    //     var pair = GetValidatedPair(token);
    //     SafeTransferFrom(OPDT, Message.Sender, pair, amount);
    //     var response = Call(pair, 0, "Stake", new object[] {Message.Sender});
    //     Assert(response.Success);
    // }
    
    public void SwapExactCRSForTokens(UInt256 amountTokenOutMin, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountOut = GetAmountOut(Message.Value, reserves[0], reserves[1]);
        Assert(amountOut >= amountTokenOutMin, "OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");
        SafeTransfer(pair, Message.Value);
        Swap(0, amountOut, pair, to);
    }
    
    public void SwapTokensForExactCRS(ulong amountCrsOut, UInt256 amountTokenInMax, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountIn = GetAmountIn(amountCrsOut, reserves[1], reserves[0]);
        Assert(amountIn <= amountTokenInMax, "OpdexV1: EXCESSIVE_INPUT_AMOUNT");
        SafeTransferFrom(token, Message.Sender, pair, amountIn);
        Swap(amountCrsOut, 0, pair, to);
    }
    
    public void SwapExactTokensForCRS(UInt256 amountTokenIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountOut = GetAmountOut(amountTokenIn, reserves[1], reserves[0]);
        Assert(amountOut >= amountCrsOutMin, "OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");  
        SafeTransferFrom(token, Message.Sender, pair, amountTokenIn);
        Swap((ulong)amountOut, 0, pair, to);
    }
    
    public void SwapCRSForExactTokens(UInt256 amountTokenOut, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
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

    private void ValidateDeadline(ulong deadline)
    {
        Assert(deadline == 0 || Block.Number <= deadline, "OpdexV1: EXPIRED_DEADLINE");
    }
}

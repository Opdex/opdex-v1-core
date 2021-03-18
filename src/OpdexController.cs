using Stratis.SmartContracts;

[Deploy]
public class OpdexController : ContractBase
{
    public OpdexController(ISmartContractState smartContractState) : base(smartContractState)
    {
        Owner = Message.Sender;
    }
    
    public Address Owner
    {
        get => State.GetAddress(nameof(Owner));
        private set => State.SetAddress(nameof(Owner), value);
    }
    
    public Address StakeToken
    {
        get => State.GetAddress(nameof(StakeToken));
        private set => State.SetAddress(nameof(StakeToken), value);
    }
    
    public Address MiningGovernance
    {
        get => State.GetAddress(nameof(MiningGovernance));
        private set => State.SetAddress(nameof(MiningGovernance), value);
    }

    public void SetOwner(Address address)
    {
        EnsureOwner();
        Owner = address;
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
        Assert(token != Address.Zero && State.IsContract(token), "OPDEX: ZERO_ADDRESS");
        
        var pair = GetPair(token);
        
        Assert(pair == Address.Zero, "OPDEX: PAIR_EXISTS");
        
        var pairContract = Create<OpdexPair>(0, new object[] {token, StakeToken});
        
        pair = pairContract.NewContractAddress;
        
        SetPair(token, pair);

        LogPairCreatedEvent(token, pair);
        
        return pair;
    }
    
    public object[] AddLiquidity(Address token, UInt256 amountSrcDesired, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline)
    { 
        ValidateDeadline(deadline);
        
        var liquidityAmounts = CalculateLiquidityAmounts(token, Message.Value, amountSrcDesired, amountCrsMin, amountSrcMin);
        var amountCrs = (ulong)liquidityAmounts[0];
        var amountSrc = (UInt256)liquidityAmounts[1];
        var pair = (Address)liquidityAmounts[2];
        
        SafeTransferFrom(token, Message.Sender, pair, amountSrc);
        
        var change = Message.Value - amountCrs;
        
        SafeTransfer(pair, amountCrs);
        
        var liquidityResponse = Call(pair, 0, "Mint", new object[] {to});
        
        Assert(liquidityResponse.Success, "OPDEX: INVALID_MINT_RESPONSE");
        
        SafeTransfer(Message.Sender, change);
        
        return new [] { amountCrs, amountSrc, liquidityResponse.ReturnValue };
    }
    
    public object[] RemoveLiquidity(Address token, UInt256 liquidity, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var pair = GetValidatedPair(token);
        
        SafeTransferFrom(pair, Message.Sender, pair, liquidity);
        
        var burnDtoResponse = Call(pair, 0, "Burn", new object[] {to});
        var burnResponse = (UInt256[])burnDtoResponse.ReturnValue;
        var receivedCrs = (ulong)burnResponse[0];
        var receivedSrc = burnResponse[1];
        
        Assert(receivedCrs >= amountCrsMin, "OPDEX: INSUFFICIENT_CRS_AMOUNT");
        Assert(receivedSrc >= amountSrcMin, "OPDEX: INSUFFICIENT_SRC_AMOUNT");
        
        return new object[] { receivedCrs, receivedSrc };
    }
    
    public void SwapExactCrsForSrc(UInt256 amountSrcOutMin, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountOut = GetAmountOut(Message.Value, reserves.ReserveCrs, reserves.ReserveSrc);
        
        Assert(amountOut >= amountSrcOutMin, "OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        
        SafeTransfer(pair, Message.Value);
        Swap(0, amountOut, pair, to);
    }
    
    public void SwapSrcForExactCrs(ulong amountCrsOut, UInt256 amountSrcInMax, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountIn = GetAmountIn(amountCrsOut, reserves.ReserveSrc, reserves.ReserveCrs);
        
        Assert(amountIn <= amountSrcInMax, "OPDEX: EXCESSIVE_INPUT_AMOUNT");
        
        SafeTransferFrom(token, Message.Sender, pair, amountIn);
        Swap(amountCrsOut, 0, pair, to);
    }
    
    public void SwapExactSrcForCrs(UInt256 amountSrcIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountOut = GetAmountOut(amountSrcIn, reserves.ReserveSrc, reserves.ReserveCrs);
        
        Assert(amountOut >= amountCrsOutMin, "OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        
        SafeTransferFrom(token, Message.Sender, pair, amountSrcIn);
        Swap((ulong)amountOut, 0, pair, to);
    }
    
    public void SwapCrsForExactSrc(UInt256 amountSrcOut, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountIn = (ulong)GetAmountIn(amountSrcOut, reserves.ReserveCrs, reserves.ReserveSrc);
        
        Assert(amountIn <= Message.Value, "OPDEX: EXCESSIVE_INPUT_AMOUNT");
        
        var change = Message.Value - amountIn;
        
        SafeTransfer(pair, amountIn);
        Swap(0, amountSrcOut, pair, to);
        SafeTransfer(Message.Sender, change);
    }

    public void SwapSrcForExactSrc(UInt256 amountSrcInMax, Address tokenIn, UInt256 amountSrcOut, Address tokenOut, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var tokenInPair = GetValidatedPair(tokenIn);
        var tokenOutPair = GetValidatedPair(tokenOut);
        var tokenInReserves = GetReserves(tokenInPair);
        var tokenOutReserves = GetReserves(tokenOutPair);
        var amounts = GetAmountIn(amountSrcOut, tokenOutReserves.ReserveCrs, tokenOutReserves.ReserveSrc, tokenInReserves.ReserveSrc, tokenInReserves.ReserveCrs);
        var amountCrs = (ulong)amounts[0];
        var amountSrc = amounts[1];

        Assert(amountSrcOut <= amountSrcInMax, "OPDEX: INSUFFICIENT_INPUT_AMOUNT");

        SafeTransferFrom(tokenIn, Message.Sender, tokenInPair, amountSrc);
        Swap(amountCrs, 0, tokenInPair, Address);
        
        SafeTransfer(tokenOutPair, amountCrs);
        Swap(0, amountSrcOut, tokenOutPair, to);
    }
    
    public void SwapExactSrcForSrc(UInt256 amountSrcIn, Address tokenIn, UInt256 amountSrcOutMin, Address tokenOut, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var tokenInPair = GetValidatedPair(tokenIn);
        var tokenOutPair = GetValidatedPair(tokenOut);
        var tokenInReserves = GetReserves(tokenInPair);
        var tokenOutReserves = GetReserves(tokenOutPair);
        var amounts = GetAmountOut(amountSrcIn, tokenInReserves.ReserveSrc, tokenInReserves.ReserveCrs, tokenOutReserves.ReserveCrs, tokenOutReserves.ReserveSrc);
        var amountCrsOut = (ulong)amounts[0];
        var amountSrcOut = amounts[1];
        
        Assert(amountSrcOutMin <= amountSrcOut, "OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        
        SafeTransferFrom(tokenIn, Message.Sender, tokenInPair, amountSrcIn);
        Swap(amountCrsOut, 0, tokenInPair, Address);
        
        SafeTransfer(tokenOutPair, amountCrsOut);
        Swap(0, amountSrcOut, tokenOutPair, to);
    }
    
    public UInt256 GetLiquidityQuote(UInt256 amountA, UInt256 reserveA, UInt256 reserveB)
    {
        Assert(amountA > 0, "OPDEX: INSUFFICIENT_AMOUNT");
        Assert(reserveA > 0 && reserveB > 0, "OPDEX: INSUFFICIENT_LIQUIDITY");
        
        return amountA * reserveB / reserveA;
    }
    
    public UInt256 GetAmountOut(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut)
    {
        Assert(amountIn > 0, "OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        Assert(reserveIn > 0 && reserveOut > 0, "OPDEX: INSUFFICIENT_LIQUIDITY");
        
        var amountInWithFee = amountIn * 997;
        var numerator = amountInWithFee * reserveOut;
        var denominator = reserveIn * 1000 + amountInWithFee;
        
        return numerator / denominator;
    }

    public UInt256 GetAmountIn(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut)
    {
        Assert(amountOut > 0, "OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        Assert(reserveIn > 0 && reserveOut > 0, "OPDEX: INSUFFICIENT_LIQUIDITY");
        
        var numerator = reserveIn * amountOut * 1000;
        var denominator = (reserveOut - amountOut) * 997;
        
        return numerator / denominator + 1;
    }

    // Todo: Tests
    public UInt256[] GetAmountIn(UInt256 amountSrcOut, UInt256 srcOutReserveCrs, UInt256 srcOutReserveSrc, 
        UInt256 crsInReserveSrc, UInt256 crsInReserveCrs)
    {
        var amountCrs = GetAmountIn(amountSrcOut, srcOutReserveCrs, srcOutReserveSrc);
        var amountSrc = GetAmountOut(amountCrs, crsInReserveCrs, crsInReserveSrc);

        return new[] {amountCrs, amountSrc};
    }
    
    // Todo: Tests
    public UInt256[] GetAmountOut(UInt256 amountSrcIn, UInt256 srcInReserveSrc, UInt256 srcInReserveCrs,  
        UInt256 crsOutReserveCrs, UInt256 crsOutReserveSrc)
    {
        var amountCrs = GetAmountOut(amountSrcIn, srcInReserveSrc, srcInReserveCrs);
        var amountSrc = GetAmountOut(amountCrs, crsOutReserveCrs, crsOutReserveSrc);

        return new[] {amountCrs, amountSrc};
    }

    public void NominateLiquidityMiner(Address token)
    {
        EnsureMiningEnabled();
        
        var pair = GetValidatedPair(token);
        
        var weightResponse = Call(pair, 0ul, "get_TotalWeight");
        var weight = (UInt256)weightResponse.ReturnValue;
        
        Assert(weight > UInt256.Zero, "OPDEX: INVALID_STAKING_WEIGHT");
        Assert(Call(MiningGovernance, 0ul, "Nominate", new object[] {pair, weight}).Success);
    }

    public void EnableStakingAndMining(Address stakingToken, Address miningGovernance)
    {
        EnsureOwner();
        
        Assert(State.IsContract(stakingToken));
        Assert(State.IsContract(miningGovernance));
        Assert(StakeToken == Address.Zero);
        Assert(MiningGovernance == Address.Zero);
        
        StakeToken = stakingToken;
        MiningGovernance = miningGovernance;
        
        SetMiningGovernanceController(Address);
    }

    public void UpdateMiningGovernanceControllerAddress(Address updatedController)
    {
        EnsureOwner();
        EnsureMiningEnabled();
        
        Assert(State.IsContract(updatedController));

        SetMiningGovernanceController(updatedController);
    }

    public void UpdateMiningGovernanceAddress(Address miningGovernance)
    {
        EnsureOwner();
        EnsureMiningEnabled();
        
        Assert(State.IsContract(miningGovernance));
        
        MiningGovernance = miningGovernance;
    }

    private void SetMiningGovernanceController(Address controller)
    {
        Assert(Call(MiningGovernance, 0ul, "SetController", new object[] {controller}).Success);
    }

    private void EnsureOwner()
    {
        Assert(Message.Sender == Owner, "OPDEX: UNAUTHORIZED");
    }

    private void EnsureMiningEnabled()
    {
        Assert(MiningGovernance != Address.Zero, "OPDEX: MINING_UNAVAILABLE");
    }
    
    private object[] CalculateLiquidityAmounts(Address token, ulong amountCrsDesired, UInt256 amountSrcDesired, ulong amountCrsMin, UInt256 amountSrcMin)
    {
        var reserves = new Reserves();
        var pair = GetPair(token);

        if (pair == Address.Zero)
        {
            pair = CreatePair(token);
        }
        else
        {
            reserves = GetReserves(pair);
        }
        
        ulong amountCrs;
        UInt256 amountSrc;
        
        if (reserves.ReserveCrs == 0 && reserves.ReserveSrc == 0)
        {
            amountCrs = amountCrsDesired;
            amountSrc = amountSrcDesired;
        }
        else
        {
            var amountSrcOptimal = GetLiquidityQuote(amountCrsDesired, reserves.ReserveCrs, reserves.ReserveSrc);
            if (amountSrcOptimal <= amountSrcDesired)
            {
                Assert(amountSrcOptimal >= amountSrcMin, "OPDEX: INSUFFICIENT_B_AMOUNT");
                amountCrs = amountCrsDesired;
                amountSrc = amountSrcOptimal;
            }
            else
            {
                var amountCrsOptimal = (ulong)GetLiquidityQuote(amountSrcDesired, reserves.ReserveSrc, reserves.ReserveCrs);
                Assert(amountCrsOptimal <= amountCrsDesired && amountCrsOptimal >= amountCrsMin, "OPDEX: INSUFFICIENT_A_AMOUNT");
                amountCrs = amountCrsOptimal;
                amountSrc = amountSrcDesired;
            }
        }
        
        return new object[] { amountCrs, amountSrc, pair };
    }
    
    private void Swap(ulong amountCrsOut, UInt256 amountSrcOut, Address pair, Address to)
    {
        var response = Call(pair, 0, "Swap", new object[] {amountCrsOut, amountSrcOut, to, new byte[0]});
        
        Assert(response.Success, "OPDEX: INVALID_SWAP_ATTEMPT");
    }

    private Reserves GetReserves(Address pair)
    {
        var reservesResponse = Call(pair, 0, "get_Reserves");
        
        Assert(reservesResponse.Success, "OPDEX: INVALID_PAIR");
        
        var reserves = (byte[][])reservesResponse.ReturnValue;
        
        return new Reserves
        {
            ReserveCrs = Serializer.ToUInt64(reserves[0]),
            ReserveSrc = Serializer.ToUInt256(reserves[1])
        };
    }
    
    private Address GetValidatedPair(Address token)
    {
        var pair = GetPair(token);
        
        Assert(pair != Address.Zero, "OPDEX: INVALID_PAIR");
        
        return pair;
    }

    private void ValidateDeadline(ulong deadline)
    {
        Assert(deadline == 0 || Block.Number <= deadline, "OPDEX: EXPIRED_DEADLINE");
    }

    private void LogPairCreatedEvent(Address token, Address pair)
    {
        Log(new OpdexPairCreatedEvent
        {
            Token = token, 
            Pair = pair,
        });
    }
}

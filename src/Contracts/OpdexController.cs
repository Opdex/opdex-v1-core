﻿using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

/// <summary>
/// Controller contract used for managing available pools and routing transactions. Validates and completes prerequisite
/// transactions necessary for adding or removing liquidity or swapping in liquidity pools.
/// </summary>
[Deploy]
public class OpdexController : SmartContract, IOpdexController
{
    /// <summary>
    /// Constructor to initialize the controller.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="stakeToken">The address of the SRC token used for staking in pools.</param>
    public OpdexController(ISmartContractState state, Address stakeToken) 
        : base(state)
    {
        StakeToken = stakeToken;
    }
    
    /// <inheritdoc />
    public Address StakeToken
    {
        get => State.GetAddress(nameof(StakeToken));
        private set => State.SetAddress(nameof(StakeToken), value);
    }

    /// <inheritdoc />
    public Address GetPool(Address token)
    {
        return State.GetAddress($"Pool:{token}");
    }

    private void SetPool(Address token, Address contract)
    {
        State.SetAddress($"Pool:{token}", contract);
    }

    /// <inheritdoc />
    public Address CreatePool(Address token)
    {
        Assert(token != Address.Zero && State.IsContract(token), "OPDEX: ZERO_ADDRESS");
        
        var pool = GetPool(token);
        
        Assert(pool == Address.Zero, "OPDEX: POOL_EXISTS");
        
        pool = Create<OpdexStakingPool>(0, new object[] {token, StakeToken}).NewContractAddress;
        
        SetPool(token, pool);
        
        Log(new OpdexPoolCreatedEvent { Token = token, Pool = pool });
        
        return pool;
    }
    
    /// <inheritdoc />
    public object[] AddLiquidity(Address token, UInt256 amountSrcDesired, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline)
    { 
        ValidateDeadline(deadline);
        
        var liquidityAmounts = CalculateLiquidityAmounts(token, Message.Value, amountSrcDesired, amountCrsMin, amountSrcMin);
        var amountCrs = (ulong)liquidityAmounts[0];
        var amountSrc = (UInt256)liquidityAmounts[1];
        var pool = (Address)liquidityAmounts[2];

        SafeTransfer(Message.Sender, Message.Value - amountCrs);
        SafeTransferFrom(token, Message.Sender, pool, amountSrc);

        var liquidityResponse = Call(pool, amountCrs, nameof(IOpdexStandardPool.Mint), new object[] {to});
        Assert(liquidityResponse.Success, "OPDEX: INVALID_MINT_RESPONSE");
        
        return new [] { amountCrs, amountSrc, liquidityResponse.ReturnValue };
    }
    
    /// <inheritdoc />
    public object[] RemoveLiquidity(Address token, UInt256 liquidity, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var pool = GetValidatedPool(token);
        
        SafeTransferFrom(pool, Message.Sender, pool, liquidity);
        
        var burnDtoResponse = Call(pool, 0, nameof(IOpdexStandardPool.Burn), new object[] {to});
        var burnResponse = (UInt256[])burnDtoResponse.ReturnValue;
        var amountCrs = (ulong)burnResponse[0];
        var amountSrc = burnResponse[1];

        Assert(amountCrs >= amountCrsMin, "OPDEX: INSUFFICIENT_CRS_AMOUNT");
        Assert(amountSrc >= amountSrcMin, "OPDEX: INSUFFICIENT_SRC_AMOUNT");
        
        return new object[] { (ulong)burnResponse[0], burnResponse[1] };
    }
    
    /// <inheritdoc />
    public void SwapExactCrsForSrc(UInt256 amountSrcOutMin, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var pool = GetValidatedPool(token);
        var reserves = GetReserves(pool);
        var amountOut = GetAmountOut(Message.Value, reserves.ReserveCrs, reserves.ReserveSrc);
        
        Assert(amountOut >= amountSrcOutMin, "OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        
        Swap(0, amountOut, pool, to, Message.Value);
    }
    
    /// <inheritdoc />
    public void SwapSrcForExactCrs(ulong amountCrsOut, UInt256 amountSrcInMax, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var pool = GetValidatedPool(token);
        var reserves = GetReserves(pool);
        var amountIn = GetAmountIn(amountCrsOut, reserves.ReserveSrc, reserves.ReserveCrs);
        
        Assert(amountIn <= amountSrcInMax, "OPDEX: EXCESSIVE_INPUT_AMOUNT");
        
        SafeTransferFrom(token, Message.Sender, pool, amountIn);
        Swap(amountCrsOut, 0, pool, to, 0);
    }
    
    /// <inheritdoc />
    public void SwapExactSrcForCrs(UInt256 amountSrcIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var pool = GetValidatedPool(token);
        var reserves = GetReserves(pool);
        var amountOut = GetAmountOut(amountSrcIn, reserves.ReserveSrc, reserves.ReserveCrs);
        
        Assert(amountOut >= amountCrsOutMin, "OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        
        SafeTransferFrom(token, Message.Sender, pool, amountSrcIn);
        Swap((ulong)amountOut, 0, pool, to, 0);
    }
    
    /// <inheritdoc />
    public void SwapCrsForExactSrc(UInt256 amountSrcOut, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var pool = GetValidatedPool(token);
        var reserves = GetReserves(pool);
        var amountIn = (ulong)GetAmountIn(amountSrcOut, reserves.ReserveCrs, reserves.ReserveSrc);
        
        Assert(amountIn <= Message.Value, "OPDEX: EXCESSIVE_INPUT_AMOUNT");
        
        SafeTransfer(Message.Sender, Message.Value - amountIn);
        Swap(0, amountSrcOut, pool, to, amountIn);
    }

    /// <inheritdoc />
    public void SwapSrcForExactSrc(UInt256 amountSrcInMax, Address tokenIn, UInt256 amountSrcOut, Address tokenOut, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var tokenInPool = GetValidatedPool(tokenIn);
        var tokenOutPool = GetValidatedPool(tokenOut);
        var tokenInReserves = GetReserves(tokenInPool);
        var tokenOutReserves = GetReserves(tokenOutPool);
        var amounts = GetAmountsIn(amountSrcOut, tokenOutReserves, tokenInReserves);
        var amountCrs = (ulong)amounts[0];
        var amountSrcIn = amounts[1];
        
        Assert(amounts[1] <= amountSrcInMax, "OPDEX: INSUFFICIENT_INPUT_AMOUNT");

        SafeTransferFrom(tokenIn, Message.Sender, tokenInPool, amountSrcIn);
        Swap(amountCrs, 0, tokenInPool, tokenOutPool, 0);
        Swap(0, amountSrcOut, tokenOutPool, to, 0);
    }
    
    /// <inheritdoc />
    public void SwapExactSrcForSrc(UInt256 amountSrcIn, Address tokenIn, UInt256 amountSrcOutMin, Address tokenOut, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        
        var tokenInPool = GetValidatedPool(tokenIn);
        var tokenOutPool = GetValidatedPool(tokenOut);
        var tokenInReserves = GetReserves(tokenInPool);
        var tokenOutReserves = GetReserves(tokenOutPool);
        var amounts = GetAmountsOut(amountSrcIn, tokenInReserves, tokenOutReserves);
        var amountCrs = (ulong)amounts[0];
        var amountSrcOut = amounts[1];
        
        Assert(amountSrcOutMin <= amountSrcOut, "OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        
        SafeTransferFrom(tokenIn, Message.Sender, tokenInPool, amountSrcIn);
        Swap(amountCrs, 0, tokenInPool, tokenOutPool, 0);
        Swap(0, amountSrcOut, tokenOutPool, to, 0);
    }
    
    /// <inheritdoc />
    public UInt256 GetLiquidityQuote(UInt256 amountA, UInt256 reserveA, UInt256 reserveB)
    {
        Assert(amountA > 0, "OPDEX: INSUFFICIENT_AMOUNT");
        Assert(reserveA > 0 && reserveB > 0, "OPDEX: INSUFFICIENT_LIQUIDITY");
        
        return amountA * reserveB / reserveA;
    }
    
    /// <inheritdoc />
    public UInt256 GetAmountOut(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut)
    {
        Assert(amountIn > 0, "OPDEX: INSUFFICIENT_INPUT_AMOUNT");
        Assert(reserveIn > 0 && reserveOut > 0, "OPDEX: INSUFFICIENT_LIQUIDITY");
        
        var amountInWithFee = amountIn * 997;
        var numerator = amountInWithFee * reserveOut;
        var denominator = reserveIn * 1000 + amountInWithFee;
        
        return numerator / denominator;
    }

    /// <inheritdoc />
    public UInt256 GetAmountOut(UInt256 tokenInAmount, UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc,
        UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc)
    {
        var tokenInReserves = new Reserves {ReserveCrs = (ulong)tokenInReserveCrs, ReserveSrc = tokenInReserveSrc};
        var tokenOutReserves = new Reserves {ReserveCrs = (ulong)tokenOutReserveCrs, ReserveSrc = tokenOutReserveSrc};

        var amounts = GetAmountsOut(tokenInAmount, tokenInReserves, tokenOutReserves);
        
        return amounts[1];
    }
    
    /// <inheritdoc />
    public UInt256 GetAmountIn(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut)
    {
        Assert(amountOut > 0, "OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");
        Assert(reserveIn > 0 && reserveOut > 0, "OPDEX: INSUFFICIENT_LIQUIDITY");
        
        var numerator = reserveIn * amountOut * 1000;
        var denominator = (reserveOut - amountOut) * 997;
        
        return numerator / denominator + 1;
    }

    /// <inheritdoc />
    public UInt256 GetAmountIn(UInt256 tokenOutAmount, UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc, 
        UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc)
    {
        var tokenInReserves = new Reserves {ReserveCrs = (ulong)tokenInReserveCrs, ReserveSrc = tokenInReserveSrc};
        var tokenOutReserves = new Reserves {ReserveCrs = (ulong)tokenOutReserveCrs, ReserveSrc = tokenOutReserveSrc};

        var amounts = GetAmountsIn(tokenOutAmount, tokenOutReserves, tokenInReserves);
        
        return amounts[1];
    }
    
    private UInt256[] GetAmountsIn(UInt256 tokenOutAmount, Reserves tokenOutReserves, Reserves tokenInReserves)
    {
        var amountCrs = GetAmountIn(tokenOutAmount, tokenOutReserves.ReserveCrs, tokenOutReserves.ReserveSrc);
        var amountSrcIn = GetAmountIn(amountCrs, tokenInReserves.ReserveSrc, tokenInReserves.ReserveCrs);

        return new[] {amountCrs, amountSrcIn};
    }
    
    private UInt256[] GetAmountsOut(UInt256 tokenInAmount, Reserves tokenInReserves, Reserves tokenOutReserves)
    {
        var amountCrs = GetAmountOut(tokenInAmount, tokenInReserves.ReserveSrc, tokenInReserves.ReserveCrs);
        var amountSrcOut = GetAmountOut(amountCrs, tokenOutReserves.ReserveCrs, tokenOutReserves.ReserveSrc);

        return new[] {amountCrs, amountSrcOut};
    }
    
    private object[] CalculateLiquidityAmounts(Address token, ulong amountCrsDesired, UInt256 amountSrcDesired, ulong amountCrsMin, UInt256 amountSrcMin)
    {
        var reserves = new Reserves();
        var pool = GetPool(token);

        if (pool == Address.Zero) pool = CreatePool(token);
        else reserves = GetReserves(pool);

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
        
        return new object[] { amountCrs, amountSrc, pool };
    }
    
    private void Swap(ulong amountCrsOut, UInt256 amountSrcOut, Address pool, Address to, ulong amountCrsIn)
    {
        var swapParams = new object[] {amountCrsOut, amountSrcOut, to, new byte[0]};
        var response = Call(pool, amountCrsIn, nameof(IOpdexStandardPool.Swap), swapParams);
        
        Assert(response.Success, "OPDEX: INVALID_SWAP_ATTEMPT");
    }

    private Reserves GetReserves(Address pool)
    {
        var reservesResponse = Call(pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}");
        
        Assert(reservesResponse.Success, "OPDEX: INVALID_POOL");
        
        var reserves = (object[])reservesResponse.ReturnValue;
        
        return new Reserves
        {
            ReserveCrs = (ulong)reserves[0],
            ReserveSrc = (UInt256)reserves[1]
        };
    }
    
    private Address GetValidatedPool(Address token)
    {
        var pool = GetPool(token);
        
        Assert(pool != Address.Zero, "OPDEX: INVALID_POOL");
        
        return pool;
    }

    private void ValidateDeadline(ulong deadline)
    {
        Assert(deadline == 0 || Block.Number <= deadline, "OPDEX: EXPIRED_DEADLINE");
    }
    
    private void SafeTransfer(Address to, ulong amount)
    {
        if (amount == 0) return;
        
        Assert(Transfer(to, amount).Success, "OPDEX: INVALID_TRANSFER");
    }
    
    private void SafeTransferFrom(Address token, Address from, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        
        var result = Call(token, 0, nameof(IStandardToken256.TransferFrom), new object[] {from, to, amount});
        
        Assert(result.Success && (bool)result.ReturnValue, "OPDEX: INVALID_TRANSFER_FROM");
    }
}

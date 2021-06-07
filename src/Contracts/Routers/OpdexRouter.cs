using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

/// <summary>
/// A contract used to quote and route multi-step transactions for swaps and adding or removing liquidity.
/// </summary>
public class OpdexRouter : SmartContract, IOpdexRouter
{
    /// <summary>
    /// Constructor initializing the router contract.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="market">The address of the market associated.</param>
    /// <param name="transactionFee">0-10 transaction fee equal to 0-1%.</param>
    /// <param name="authProviders">Flag indicating if liquidity providers should be authorized.</param>
    /// <param name="authTraders">Flag indicating if traders should be authorized.</param>
    public OpdexRouter(ISmartContractState state, Address market, uint transactionFee, bool authProviders, bool authTraders) : base(state)
    {
        Assert(transactionFee <= 10, "OPDEX: INVALID_TRANSACTION_FEE");
        Market = market;
        TransactionFee = transactionFee;
        AuthProviders = authProviders;
        AuthTraders = authTraders;
    }

    /// <inheritdoc />
    public Address Market
    {
        get => State.GetAddress(RouterStateKeys.Market);
        private set => State.SetAddress(RouterStateKeys.Market, value);
    }

    /// <inheritdoc />
    public uint TransactionFee
    {
        get => State.GetUInt32(RouterStateKeys.TransactionFee);
        private set => State.SetUInt32(RouterStateKeys.TransactionFee, value);
    }

    /// <inheritdoc />
    public bool AuthTraders
    {
        get => State.GetBool(RouterStateKeys.AuthTraders);
        private set => State.SetBool(RouterStateKeys.AuthTraders, value);
    }

    /// <inheritdoc />
    public bool AuthProviders
    {
        get => State.GetBool(RouterStateKeys.AuthProviders);
        private set => State.SetBool(RouterStateKeys.AuthProviders, value);
    }

    /// <inheritdoc />
    public Address GetPool(Address token)
    {
        return State.GetAddress($"{RouterStateKeys.Pool}:{token}");
    }

    private void SetPool(Address token, Address pool)
    {
        State.SetAddress($"{RouterStateKeys.Pool}:{token}", pool);
    }

    /// <inheritdoc />
    public UInt256[] AddLiquidity(Address token, UInt256 amountSrcDesired, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Provide);

        var pool = GetValidatedPool(token);
        var amounts = CalculateLiquidityAmounts(pool, Message.Value, amountSrcDesired, amountCrsMin, amountSrcMin);
        var amountCrs = (ulong)amounts[0];
        var amountSrc = amounts[1];

        // Return change
        SafeTransfer(Message.Sender, Message.Value - amountCrs);
        SafeTransferFrom(token, Message.Sender, pool, amountSrc);

        var liquidityResponse = Call(pool, amountCrs, nameof(IOpdexStandardPool.Mint), new object[] {to});
        Assert(liquidityResponse.Success, "OPDEX: INVALID_MINT_RESPONSE");

        return new [] { amountCrs, amountSrc, (UInt256)liquidityResponse.ReturnValue };
    }

    /// <inheritdoc />
    public UInt256[] RemoveLiquidity(Address token, UInt256 liquidity, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Provide);

        var pool = GetValidatedPool(token);

        SafeTransferFrom(pool, Message.Sender, pool, liquidity);

        var burnDtoResponse = Call(pool, 0, nameof(IOpdexStandardPool.Burn), new object[] {to});
        var burnResponse = (UInt256[])burnDtoResponse.ReturnValue;
        var amountCrs = (ulong)burnResponse[0];
        var amountSrc = burnResponse[1];

        Assert(amountCrs >= amountCrsMin, "OPDEX: INSUFFICIENT_CRS_AMOUNT");
        Assert(amountSrc >= amountSrcMin, "OPDEX: INSUFFICIENT_SRC_AMOUNT");

        return burnResponse;
    }

    /// <inheritdoc />
    public UInt256 SwapExactCrsForSrc(UInt256 amountSrcOutMin, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Trade);

        var pool = GetValidatedPool(token);
        var reserves = GetReserves(pool);
        var amountOut = GetAmountOut(Message.Value, reserves.ReserveCrs, reserves.ReserveSrc);

        Assert(amountOut >= amountSrcOutMin, "OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");

        Swap(0, amountOut, pool, to, Message.Value);

        return amountOut;
    }

    /// <inheritdoc />
    public UInt256 SwapSrcForExactCrs(ulong amountCrsOut, UInt256 amountSrcInMax, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Trade);

        var pool = GetValidatedPool(token);
        var reserves = GetReserves(pool);
        var amountIn = GetAmountIn(amountCrsOut, reserves.ReserveSrc, reserves.ReserveCrs);

        Assert(amountIn <= amountSrcInMax, "OPDEX: EXCESSIVE_INPUT_AMOUNT");

        SafeTransferFrom(token, Message.Sender, pool, amountIn);
        Swap(amountCrsOut, 0, pool, to, 0);

        return amountIn;
    }

    /// <inheritdoc />
    public ulong SwapExactSrcForCrs(UInt256 amountSrcIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Trade);

        var pool = GetValidatedPool(token);
        var reserves = GetReserves(pool);
        var amountOut = (ulong)GetAmountOut(amountSrcIn, reserves.ReserveSrc, reserves.ReserveCrs);

        Assert(amountOut >= amountCrsOutMin, "OPDEX: INSUFFICIENT_OUTPUT_AMOUNT");

        SafeTransferFrom(token, Message.Sender, pool, amountSrcIn);
        Swap(amountOut, 0, pool, to, 0);

        return amountOut;
    }

    /// <inheritdoc />
    public ulong SwapCrsForExactSrc(UInt256 amountSrcOut, Address token, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Trade);

        var pool = GetValidatedPool(token);
        var reserves = GetReserves(pool);
        var amountIn = (ulong)GetAmountIn(amountSrcOut, reserves.ReserveCrs, reserves.ReserveSrc);

        Assert(amountIn <= Message.Value, "OPDEX: EXCESSIVE_INPUT_AMOUNT");

        // Return change
        SafeTransfer(Message.Sender, Message.Value - amountIn);
        Swap(0, amountSrcOut, pool, to, amountIn);

        return amountIn;
    }

    /// <inheritdoc />
    public UInt256 SwapSrcForExactSrc(UInt256 amountSrcInMax, Address tokenIn, UInt256 amountSrcOut, Address tokenOut, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Trade);

        var tokenInPool = GetValidatedPool(tokenIn);
        var tokenOutPool = GetValidatedPool(tokenOut);
        var tokenInReserves = GetReserves(tokenInPool);
        var tokenOutReserves = GetReserves(tokenOutPool);
        var amounts = GetAmountsIn(amountSrcOut, tokenOutReserves, tokenInReserves);
        var amountCrs = (ulong)amounts[0];
        var amountSrcIn = amounts[1];

        Assert(amountSrcIn <= amountSrcInMax, "OPDEX: EXCESSIVE_INPUT_AMOUNT");

        SafeTransferFrom(tokenIn, Message.Sender, tokenInPool, amountSrcIn);
        Swap(amountCrs, 0, tokenInPool, tokenOutPool, 0);
        Swap(0, amountSrcOut, tokenOutPool, to, 0);

        return amountSrcIn;
    }

    /// <inheritdoc />
    public UInt256 SwapExactSrcForSrc(UInt256 amountSrcIn, Address tokenIn, UInt256 amountSrcOutMin, Address tokenOut, Address to, ulong deadline)
    {
        ValidateDeadline(deadline);
        EnsureAuthorizationFor(Message.Sender, to, Permissions.Trade);

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

        return amountSrcOut;
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

        const uint offset = 1_000;
        var amountInWithTransactionFee = amountIn * (offset - TransactionFee);
        var numerator = amountInWithTransactionFee * reserveOut;
        var denominator = reserveIn * offset + amountInWithTransactionFee;

        return numerator / denominator;
    }

    /// <inheritdoc />
    public UInt256 GetAmountOut(UInt256 tokenInAmount, UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc, UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc)
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

        const uint offset = 1_000;
        var numerator = reserveIn * amountOut * offset;
        var denominator = (reserveOut - amountOut) * (offset - TransactionFee);

        return numerator / denominator + 1;
    }

    /// <inheritdoc />
    public UInt256 GetAmountIn(UInt256 tokenOutAmount, UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc, UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc)
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

    private UInt256[] CalculateLiquidityAmounts(Address pool, ulong amountCrsDesired, UInt256 amountSrcDesired, ulong amountCrsMin, UInt256 amountSrcMin)
    {
        var reserves = GetReserves(pool);

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

        return new [] { amountCrs, amountSrc };
    }

    private void Swap(ulong amountCrsOut, UInt256 amountSrcOut, Address pool, Address to, ulong amountCrsIn)
    {
        var swapParams = new object[] {amountCrsOut, amountSrcOut, to, new byte[0]};
        var response = Call(pool, amountCrsIn, nameof(IOpdexStandardPool.Swap), swapParams);

        Assert(response.Success, "OPDEX: INVALID_SWAP_ATTEMPT");
    }

    private Address GetValidatedPool(Address token)
    {
        var pool = GetPool(token);

        if (pool != Address.Zero) return pool;

        var poolResponse = Call(Market, 0, nameof(IOpdexMarket.GetPool), new object[] {token});

        Assert(poolResponse.Success && (Address)poolResponse.ReturnValue != Address.Zero, "OPDEX: INVALID_POOL");

        pool = (Address)poolResponse.ReturnValue;

        SetPool(token, pool);

        return pool;
    }

    private Reserves GetReserves(Address pool)
    {
        var reservesResponse = Call(pool, 0, $"get_{nameof(IOpdexStandardPool.Reserves)}");

        Assert(reservesResponse.Success, "OPDEX: INVALID_POOL");

        var reserves = (UInt256[])reservesResponse.ReturnValue;

        return new Reserves
        {
            ReserveCrs = (ulong)reserves[0],
            ReserveSrc = reserves[1]
        };
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

    private void EnsureAuthorizationFor(Address sender, Address recipient, Permissions permission)
    {
        // Skip auth if the sender is the recipient, the liquidity pool will authorize them
        if (sender == recipient) return;

        // Skip auth if it is not required for the associated market
        if (permission == Permissions.Trade && !AuthTraders) return;
        if (permission == Permissions.Provide && !AuthProviders) return;

        var authParams = new object[] {Message.Sender, (byte)permission};
        var isAuthorizedResponse = Call(Market, 0, nameof(IOpdexStandardMarket.IsAuthorized), authParams);

        Assert(isAuthorizedResponse.Success && (bool)isAuthorizedResponse.ReturnValue, "OPDEX: UNAUTHORIZED");
    }
}
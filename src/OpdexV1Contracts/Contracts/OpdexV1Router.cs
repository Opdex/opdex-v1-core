using Stratis.SmartContracts;

[Deploy]
public class OpdexV1Router : SmartContract
{
    public OpdexV1Router(ISmartContractState smartContractState, Address feeToSetter, Address feeTo) : base(smartContractState)
    {
        FeeToSetter = feeToSetter;
        FeeTo = feeTo;
    }
    
    private void SetPair(Address token, Address contract) 
        => PersistentState.SetAddress($"Pair:{token}", contract);

    public Address GetPair(Address token) 
        => PersistentState.GetAddress($"Pair:{token}");

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

    # region Liquidity
    
    public AddLiquidityResponseModel AddLiquidity(Address token, ulong amountTokenDesired, ulong amountCrsMin, ulong amountTokenMin, Address to, ulong deadline)
    {
        var liquidityDto = CalcLiquidity(token, Message.Value, amountTokenDesired, amountCrsMin, amountTokenMin);
        
        // Pull tokens from sender
        SafeTransferFrom(token, Message.Sender, liquidityDto.Pair, liquidityDto.AmountToken);
        
        // Deposit (transfer) sent CRS
        SafeTransfer(liquidityDto.Pair, liquidityDto.AmountCrs);
        
        // Call Pair Contract, mint LP tokens for sender
        var liquidityResponse = Call(liquidityDto.Pair, 0, "Mint", new object[] {to});
        
        // Transfer any change back to sender
        SafeTransfer(Message.Sender, Message.Value - liquidityDto.AmountCrs);

        return new AddLiquidityResponseModel
        {
            AmountCrs = liquidityDto.AmountCrs,
            AmountToken = liquidityDto.AmountToken,
            Liquidity = (ulong)liquidityResponse.ReturnValue
        };
    }
    
    // Todo: This entire method can likely be refactored to be more gas performant
    public RemoveLiquidityResponseModel RemoveLiquidity(Address token, ulong liquidity, ulong amountCrsMin, ulong amountTokenMin, Address to, ulong deadline)
    {
        var pair = GetAndValidatePairExists(token);

        // Todo: Can the transferFrom and burn call to the pair use/take advantage of a shared method saving 1 call
        // Send liquidity to pair
        SafeTransferFrom(pair, Message.Sender, pair, liquidity);
        
        // Burn liquidity tokens
        var burnDtoResponse = Call(pair, 0, "Burn", new object[] {to});
        var burnDto = (BurnDto)burnDtoResponse.ReturnValue;

        Assert(burnDto.AmountCrs >= amountCrsMin, "OpdexV1: INSUFFICIENT_CRS_AMOUNT");
        Assert(burnDto.AmountToken >= amountTokenMin, "OpdexV1: INSUFFICIENT_TOKEN_AMOUNT");
        
        // Todo: Should these methods happen here at all? 
        // Transfer tokens from this contract to destination
        SafeTransferTo(token, to, burnDto.AmountToken);
        
        // Transfer the CRS to it's destination
        Transfer(to, burnDto.AmountCrs);

        return new RemoveLiquidityResponseModel
        {
            AmountCrs = burnDto.AmountCrs,
            AmountToken = burnDto.AmountToken
        };
    }
    
    private CalcLiquidityDto CalcLiquidity(Address token, ulong amountCrsDesired, ulong amountTokenDesired, ulong amountCrsMin, ulong amountTokenMin)
    {
        ReservesDto reserves;

        var pair = GetPair(token);
        if (pair != Address.Zero)
        {
            reserves = GetReserves(pair);
        }
        else
        {
            pair = CreatePair(token);
            reserves = new ReservesDto
            {
                ReserveCrs = 0,
                ReserveToken = 0
            };
        }

        ulong amountCrs;
        ulong amountToken;
        
        if (reserves.ReserveCrs == 0 && reserves.ReserveToken == 0)
        {
            amountCrs = amountCrsDesired;
            amountToken = amountTokenDesired;
        }
        else
        {
            var amountTokenOptimal = GetLiquidityQuote(amountCrsDesired, reserves.ReserveCrs, reserves.ReserveToken);
            if (amountTokenOptimal <= amountTokenDesired)
            {
                Assert(amountTokenOptimal >= amountTokenMin, "OpdexV1: INSUFFICIENT_B_AMOUNT");
                amountCrs = amountCrsDesired;
                amountToken = amountTokenOptimal;
            }
            else
            {
                var amountCrsOptimal = GetLiquidityQuote(amountTokenDesired, reserves.ReserveToken, reserves.ReserveCrs);
                Assert(amountCrsOptimal <= amountCrsDesired && amountCrsOptimal >= amountCrsMin, "OpdexV1: INSUFFICIENT_A_AMOUNT");
                amountCrs = amountCrsOptimal;
                amountToken = amountTokenDesired;
            }
        }

        return new CalcLiquidityDto
        {
            AmountCrs = amountCrs,
            AmountToken = amountToken,
            Pair = pair
        };
    }
    
    #endregion
    
    #region Swaps

    // Todo: Maybe enable these for a single hop SRC -> CRS -> SRC swap
    // Single hop limit possibility, will need to stay under gas limits per transaction
    // public void SwapExactTokensForTokens()
    // {
    // }
    
    // public void SwapTokensForExactTokens()
    // {
    // }

    // Todo: Support potential future fee on transfer tokens
    // fee on transfer tokens on Stratis do not exist yet but _when_ they do,
    // transfers may burn a bit of the supply so the transferred balance after a swap 
    // Needs to be equal to the balance of the address after its been transferred from
    // the pair contract to the router contract

    /// <summary>
    /// Equivalent to a CRS sell (e.g. Sell exactly 1 CRS for about 10 OPD)
    /// </summary>
    /// <param name="amountTokenOutMin"></param>
    /// <param name="token"></param>
    /// <param name="to"></param>
    /// <param name="deadline"></param>
    public void SwapExactCRSForTokens(ulong amountTokenOutMin, Address token, Address to, ulong deadline)
    {
        var pair = GetAndValidatePairExists(token);
        var reserves = GetReserves(pair);
        var amountOut = GetAmountOut(Message.Value, reserves.ReserveCrs, reserves.ReserveToken);
        Assert(amountOut >= amountTokenOutMin, "OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");
        SafeTransfer(pair, Message.Value);
        Swap(0, amountOut, pair, to);
    }
    
    /// <summary>
    /// Equivalent to a SRC sell (e.g. Sell about 10 OPD for exactly 1 CRS)
    /// </summary>
    /// <param name="amountCrsOut"></param>
    /// <param name="amountTokenInMax"></param>
    /// <param name="token"></param>
    /// <param name="to"></param>
    /// <param naem="deadline"></param>
    public void SwapTokensForExactCRS(ulong amountCrsOut, ulong amountTokenInMax, Address token, Address to, ulong deadline)
    {
        var pair = GetAndValidatePairExists(token);
        var reserves = GetReserves(pair);
        var amountIn = GetAmountIn(amountCrsOut, reserves.ReserveToken, reserves.ReserveCrs);
        Assert(amountIn <= amountTokenInMax, "OpdexV1: EXCESSIVE_INPUT_AMOUNT");
        SafeTransferFrom(token, Message.Sender, pair, amountIn);
        Swap(amountCrsOut, 0, pair, to);
    }
    
    /// <summary>
    /// Equivalent to a SRC sell (e.g. Sell exactly 10 OPD for about 1 CRS)
    /// </summary>
    /// <param name="amountTokenIn"></param>
    /// <param name="amountCrsOutMin"></param>
    /// <param name="token"></param>
    /// <param name="to"></param>
    /// <param name="deadline"></param>
    public void SwapExactTokensForCRS(ulong amountTokenIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline)
    {
        var pair = GetAndValidatePairExists(token);
        var reserves = GetReserves(pair);
        var amountOut = GetAmountOut(amountTokenIn, reserves.ReserveToken, reserves.ReserveCrs);
        Assert(amountOut >= amountCrsOutMin, "OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");  
        SafeTransferFrom(token, Message.Sender, pair, amountTokenIn);
        Swap(amountOut, 0, pair, to);
    }
    
    /// <summary>
    /// Equivalent to a CRS sell (e.g. Sell about 1 CRS for exactly 10 OPD)
    /// </summary>
    /// <param name="amountTokenOut"></param>
    /// <param name="token"></param>
    /// <param name="to"></param>
    /// <param name="deadline"></param>
    public void SwapCRSForExactTokens(ulong amountTokenOut, Address token, Address to, ulong deadline)
    {
        var pair = GetAndValidatePairExists(token);
        var reserves = GetReserves(pair);
        var amountIn = GetAmountIn(amountTokenOut, reserves.ReserveCrs, reserves.ReserveToken);
        Assert(amountIn <= Message.Value, "OpdexV1: EXCESSIVE_INPUT_AMOUNT");
        var change = Message.Value - amountIn;
        SafeTransfer(pair, amountIn);
        Swap(0, amountTokenOut, pair, to);
        SafeTransfer(Message.Sender, change);
    }
    
    // No hops for now due to gas limits.
    // All swaps go through CRS, multiple transactions can be made to 
    // make transactions from SRC => CRS => SRC
    private void Swap(ulong amountCrsOut, ulong amountTokenOut, Address pair, Address to)
    {
        var response = Call(pair, 0, "Swap", new object[] {amountCrsOut, amountTokenOut, to});
        Assert(response.Success, "OpdexV1: INVALID_SWAP_ATTEMPT");
    }
    
    #endregion
    
    #region Public Helpers
    
    public ulong GetLiquidityQuote(ulong amountA, ulong reserveA, ulong reserveB)
    {
        Assert(amountA > 0, "OpdexV1: INSUFFICIENT_AMOUNT");
        Assert(reserveA > 0 && reserveB > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        
        return SafeMath.Div(SafeMath.Mul(amountA, reserveB), reserveA);
    }

    public ulong GetAmountOut(ulong amountIn, ulong reserveIn, ulong reserveOut)
    {
        Assert(amountIn > 0, "OpdexV1: INSUFFICIENT_INPUT_AMOUNT");
        Assert(reserveIn > 0 && reserveOut > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        
        var amountInWithFee = SafeMath.Mul(amountIn, 997);
        var numerator = SafeMath.Mul(amountInWithFee, reserveOut);
        var denominator = SafeMath.Add(SafeMath.Mul(reserveIn, 1000), amountInWithFee);
        
        return SafeMath.Div(numerator, denominator);
    }

    public ulong GetAmountIn(ulong amountOut, ulong reserveIn, ulong reserveOut)
    {
        Assert(amountOut > 0, "OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");
        Assert(reserveIn > 0 && reserveOut > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        
        var numerator = SafeMath.Mul(SafeMath.Mul(reserveIn, amountOut), 1000);
        var denominator = SafeMath.Mul(SafeMath.Sub(reserveOut, amountOut), 997);
        
        return SafeMath.Add(SafeMath.Div(numerator, denominator), 1);
    }

    // Todo: Depends on gas limitations, even single hop may be too much
    // public static void GetAmountsOut()
    // {
    // }
    //
    // public static void GetAmountsIn()
    // {
    // }
    
    #endregion
    
    #region Private Helpers

    private Address GetAndValidatePairExists(Address token)
    {
        var pair = GetPair(token);
        Assert(pair != Address.Zero, "OpdexV1: INVALID_PAIR");
        return pair;
    }

    /// <summary>
    /// Transfers CRS tokens to an address
    /// </summary>
    /// <param name="to">The address to send tokens to</param>
    /// <param name="amount">The amount to send</param>
    private void SafeTransfer(Address to, ulong amount)
    {
        if (amount == 0) return; 
        var result = Transfer(to, amount);
        Assert(result.Success, "OpdexV1: INVALID_TRANSFER");
    }
    
    /// <summary>
    /// Calls SRC TransferTo method and validates the response
    /// </summary>
    /// <param name="token">The src token contract address</param>
    /// <param name="to">The address to transfer tokens to</param>
    /// <param name="amount">The amount to transfer</param>
    private void SafeTransferTo(Address token, Address to, ulong amount)
    {
        var result = Call(token, 0, "TransferTo", new object[] {to, amount});
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_TO");
    }

    /// <summary>
    /// Calls SRC TransferFrom method and validates the response.
    /// </summary>
    /// <param name="token">The src token contract address</param>
    /// <param name="from">The approvers address</param>
    /// <param name="to">Address to transfer tokens to</param>
    /// <param name="amount">The amount to transfer</param>
    private void SafeTransferFrom(Address token, Address from, Address to, ulong amount)
    {
        var result = Call(token, 0, "TransferFrom", new object[] {from, to, amount});
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_FROM");
    }
    
    private ReservesDto GetReserves(Address pair)
    {
        var reservesResponse = Call(pair, 0, "GetReserves");
        Assert(reservesResponse.Success, "OpdexV1: INVALID_PAIR");
        return (ReservesDto)reservesResponse.ReturnValue;
    }

    #endregion

    #region Models

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

    public struct BurnDto
    {
        public ulong AmountCrs;
        public ulong AmountToken;
    }

    public struct ReservesDto
    {
        public ulong ReserveCrs;
        public ulong ReserveToken;
    }
    
    private struct CalcLiquidityDto
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

    #endregion
}
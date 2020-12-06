using Stratis.SmartContracts;

[Deploy]
public class OpdexV1Router : SmartContract
{
    public OpdexV1Router(ISmartContractState smartContractState, Address feeToSetter, Address feeTo) : base(smartContractState)
    {
        FeeToSetter = feeToSetter;
        FeeTo = feeTo;
    }
    
    private void SetPair(Address token, Address contract) => PersistentState.SetAddress($"Pair:{token}", contract);

    public Address GetPair(Address token) => PersistentState.GetAddress($"Pair:{token}");

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
        Assert(token != Address.Zero, "OpdexV1: ZERO_ADDRESS");
        var pair = GetPair(token);
        Assert(pair == Address.Zero, "OpdexV1: PAIR_EXISTS");
        var pairContract = Create<OpdexV1Pair>();
        pair = pairContract.NewContractAddress;
        SetPair(token, pair);
        // Track list of all pairs?
        Log(new PairCreatedEvent { Token = token, Pair = pair });
        return pair;
    }

    # region Liquidity
    
    public AddLiquidityResponseModel AddLiquidity(Address token, ulong amountTokenDesired, ulong amountCrsMin, ulong amountTokenMin, Address to, ulong deadline)
    {
        var liquidityDto = CalcLiquidity(token, Message.Value, amountTokenDesired, amountCrsMin, amountTokenMin);

        var pair = GetPair(token);
        
        // Pull tokens from sender - move to safe transfer method - assert validity checks
        SafeTransferFrom(token, Message.Sender, pair, liquidityDto.AmountToken);
        
        // Deposit (transfer) sent CRS
        SafeTransfer(pair, Message.Value);
        
        // Call Pair Contract, mint LP tokens for sender
        var liquidityResponse = Call(pair, 0, "Mint", new object[] {to});

        return new AddLiquidityResponseModel
        {
            AmountCrs = liquidityDto.AmountCrs,
            AmountToken = liquidityDto.AmountToken,
            Liquidity = (ulong)liquidityResponse.ReturnValue
        };
    }
    
    public RemoveLiquidityResponseModel RemoveLiquidity(Address token, ulong liquidity, ulong amountCrsMin, ulong amountTokenMin, Address to, ulong deadline)
    {
        var pair = GetPair(token);

        // Send liquidity to pair
        SafeTransferFrom(pair, Message.Sender, pair, liquidity);
        
        var burnDtoResponse = Call(pair, 0, "Burn", new object[] {to});
        var burnDto = (BurnDto)burnDtoResponse.ReturnValue;

        Assert(burnDto.AmountCrs >= amountCrsMin, "OpdexV1: INSUFFICIENT_CRS_AMOUNT");
        Assert(burnDto.AmountToken >= amountTokenMin, "OpdexV1: INSUFFICIENT_TOKEN_AMOUNT");
        
        // Transfer tokens from this contract to destination
        SafeTransferTo(token, to, burnDto.AmountToken);
        
        // Transfer the CRS to it's destination
        // Todo: Should this transfer this entire contracts Balance?
        // This contract should never hold a balance outside of each individual call
        Transfer(to, burnDto.AmountCrs);

        return new RemoveLiquidityResponseModel
        {
            AmountCrs = burnDto.AmountCrs,
            AmountToken = burnDto.AmountToken
        };
    }
    
    private CalcLiquidityDto CalcLiquidity(Address token, ulong amountCrsDesired, ulong amountTokenDesired, ulong amountCrsMin, ulong amountTokenMin)
    {
        var pair = GetPair(token);

        if (pair == Address.Zero) pair = CreatePair(token);

        var reservesDtoResponse = Call(pair, 0, "GetReserves");
        var reservesDto = (ReservesDto)reservesDtoResponse.ReturnValue;

        ulong amountCrs = 0;
        ulong amountToken = 0;
        
        if (reservesDto.ReserveCrs == 0 && reservesDto.ReserveToken == 0)
        {
            amountCrs = amountCrsDesired;
            amountToken = amountTokenDesired;
        }
        else
        {
            ulong amountTokenOptimal = 0; // Get Quote for amountADesired
            if (amountTokenOptimal <= amountTokenDesired)
            {
                Assert(amountTokenOptimal >= amountTokenMin, "OpdexV1: INSUFFICIENT_B_AMOUNT");
                amountCrs = amountCrsDesired;
                amountToken = amountTokenOptimal;
            }
            else
            {
                ulong amountCrsOptimal = 0; // Get quote for amountBDesired
                Assert(amountCrsOptimal <= amountCrsDesired && amountCrsOptimal >= amountCrsMin, "OpdexV1: INSUFFICIENT_A_AMOUNT");
                amountCrs = amountCrsOptimal;
                amountToken = amountTokenDesired;
            }
        }

        return new CalcLiquidityDto
        {
            AmountCrs = amountCrs,
            AmountToken = amountToken
        };
    }
    
    #endregion
    
    #region Swaps

    // Todo: Maybe enable these for a single hop SRC -> CRS -> SRC swap
    // Single hop limit possibility, will need to stay under gas limits per transaction
    // public void SwapExactTokensForTokens()
    // {
    //     
    // }
    
    // public void SwapTokensForExactTokens()
    // {
    //     
    // }

    // Equivalent to a CRS sell
    // Swap exactly 1 CRS for 10 OPD
    public void SwapExactCRSForTokens()
    {
        
    }

    // Equivalent to a SRC sell
    // Swap 10 OPD for exactly 1 CRS
    public void SwapTokensForExactCRS()
    {
        
    }

    // Equivalent to a SRC sell
    // Swap exactly 10 OPD for 1 CRS
    public void SwapExactTokensForCRS()
    {
        
    }

    // Equivalent to a CRS sell
    // Swap 1 CRS for exactly 10 OPD
    public void SwapCRSForExactTokens()
    {
        
    }

    // Adjust logic to allow for 1 hop or allow as many as input and just let the 
    // GasLimitExceededException get thrown?
    private void Swap(ulong[] amounts, Address[] path, Address _to)
    {
        for (uint i = 0; i < path.Length - 1; i++)
        {
            var input = path[i];
            var output = path[i + 1];
            
            // Sort tokens decides between input vs output
            // Allows for tokenA and tokenB based params to be in whatever order for the pair. Orders tokens and amounts respectively 
            
            // incorrectly just setting input temporarily
            var token0 = input;
            
            var amountOut = amounts[i + 1];
            var amount0Out = input == token0 ? 0 : amountOut;
            var amount1Out = input == token0 ? amountOut : 0;
            
            // Using PairFor, select to, setting Addrss.Zero temporarily.
            var to = i < path.Length - 2 ? Address.Zero : _to;
            
            // Get Pair
            var pair = Address.Zero;
            
            Call(pair, 0, "Swap", new object[] {amount0Out, amount1Out, to});
        }
    }
    
    #endregion
    
    #region Private Helpers

    /// <summary>
    /// Transfers CRS tokens to an address
    /// </summary>
    /// <param name="to">The address to send tokens to</param>
    /// <param name="amount">The amount to send</param>
    private void SafeTransfer(Address to, ulong amount)
    {
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
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_FROM");
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

    private struct BurnDto
    {
        public ulong AmountCrs;
        public ulong AmountToken;
    }

    private struct ReservesDto
    {
        public ulong ReserveCrs;
        public ulong ReserveToken;
    }
    
    private struct CalcLiquidityDto
    {
        public ulong AmountCrs;
        public ulong AmountToken;
    }

    public struct PairCreatedEvent
    {
        public Address Token;
        public Address Pair;
    }

    #endregion
}
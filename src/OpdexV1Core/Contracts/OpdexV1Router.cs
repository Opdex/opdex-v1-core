using Stratis.SmartContracts;

public class OpdexV1Router : SmartContract
{
    public OpdexV1Router(ISmartContractState smartContractState, Address factory, Address wcrs) : base(smartContractState)
    {
        Factory = factory;
        WCRS = wcrs;
    }

    public override void Receive()
    {
        Assert(Message.Sender == WCRS, "OpdexV2: UNACCEPTED_CRS");
        base.Receive();
    }

    public Address Factory
    {
        get => PersistentState.GetAddress(nameof(Factory));
        set => PersistentState.SetAddress(nameof(Factory), value);
    }
    
    public Address WCRS
    {
        get => PersistentState.GetAddress(nameof(WCRS));
        set => PersistentState.SetAddress(nameof(WCRS), value);
    }

    # region Liquidity
    public AddLiquidityResponseModel AddLiquidity(Address tokenA, Address tokenB, ulong amountADesired, ulong amountBDesired, ulong amountAMin, ulong amountBMin, Address to, ulong deadline)
    {
        var liquidityDto = CalcLiquidity(tokenA, tokenB, amountADesired, amountBDesired, amountAMin, amountBMin);

        var pairResponse = Call(Factory, 0, "GetPair", new object[] {tokenA, tokenB});
        var pair = (Address)pairResponse.ReturnValue;
        
        // Pull tokens from sender - move to safe transfer method - assert validity checks
        Call(tokenA, 0, "TransferFrom", new object[] {Message.Sender, pair, liquidityDto.AmountA});
        Call(tokenB, 0, "TransferFrom", new object[] {Message.Sender, pair, liquidityDto.AmountB});
        
        // Call Pair Contract, mint LP tokens for sender
        var liquidityResponse = Call(pair, 0, "Mint", new object[] {to});

        return new AddLiquidityResponseModel
        {
            AmountA = liquidityDto.AmountA,
            AmountB = liquidityDto.AmountB,
            Liquidity = (ulong)liquidityResponse.ReturnValue
        };
    }

    public AddLiquidityCRSResponseModel AddLiquidityCRS(Address token, ulong amountTokenDesired, ulong amountTokenMin, ulong amountCRSMin, Address to, ulong deadline)
    {
        var liquidityDto = CalcLiquidity(token, WCRS, amountTokenDesired, Message.Value, amountTokenMin, amountCRSMin);

        var pairResponse = Call(Factory, 0, "GetPair", new object[] {token, WCRS});
        var pair = (Address)pairResponse.ReturnValue;
        
        Call(token, 0, "TransferFrom", new object[] {Message.Sender, pair, liquidityDto.AmountA});
        
        // Deposit (transfer) sent CRS to WCRS
        
        var mintResponse = Call(pair, 0, "Mint", new object[] {to});
        
        return new AddLiquidityCRSResponseModel
        {
            AmountToken = liquidityDto.AmountA,
            AmountCRS = liquidityDto.AmountB,
            Liquidity = (ulong)mintResponse.ReturnValue
        };
    }

    private CalcLiquidityDto CalcLiquidity(Address tokenA, Address tokenB, ulong amountADesired, ulong amountBDesired, ulong amountAMin, ulong amountBMin)
    {
        var pairResponse = Call(Factory, 0, "GetPair", new object[] {tokenA, tokenB});
        var pair = (Address)pairResponse.ReturnValue;

        if (pair == Address.Zero)
        {
            var createResponse = Call(Factory, 0, "CreatePair", new object[] {tokenA, tokenB});
            pair = (Address)createResponse.ReturnValue;
        }

        var reservesDtoResponse = Call(Factory, 0, "GetReserves", new object[] {pair});
        var reservesDto = (ReservesDto)reservesDtoResponse.ReturnValue;

        ulong amountA = 0;
        ulong amountB = 0;
        
        if (reservesDto.ReserveA == 0 && reservesDto.ReserveB == 0)
        {
            amountA = amountADesired;
            amountB = amountBDesired;
        }
        else
        {
            ulong amountBOptimal = 0; // Get Quote for amountADesired
            if (amountBOptimal <= amountBDesired)
            {
                Assert(amountBOptimal >= amountBMin, "OpdexV1: INSUFFICIENT_B_AMOUNT");
                amountA = amountADesired;
                amountB = amountBOptimal;
            }
            else
            {
                ulong amountAOptimal = 0; // Get quote for amountBDesired
                Assert(amountAOptimal <= amountADesired && amountAOptimal >= amountAMin, "OpdexV1: INSUFFICIENT_A_AMOUNT");
                amountA = amountAOptimal;
                amountB = amountBDesired;
            }
        }

        return new CalcLiquidityDto
        {
            AmountA = amountA,
            AmountB = amountB
        };
    }

    public RemoveLiquidityResponseModel RemoveLiquidity(Address tokenA, Address tokenB, ulong liquidity, ulong amountAMin, ulong amountBMin, Address to, ulong deadline)
    {
        var pairResponse = Call(Factory, 0, "GetPair", new object[] {tokenA, tokenB});
        var pair = (Address)pairResponse.ReturnValue;

        // Send liquidity to pair
        Call(pair, 0, "TransferFrom", new object[] {Message.Sender, pair, liquidity});
        
        // Burn
        var burnDtoResponse = Call(pair, 0, "Burn", new object[] {to});
        var burnDto = (BurnDto)burnDtoResponse.ReturnValue;
        
        // Sort tokens
        // Allows for tokenA and tokenB based params to be in whatever order for the pair. Orders tokens and amounts respectively 
        
        Assert(burnDto.AmountA >= amountAMin, "OpdexV1: INSUFFICIENT_A_AMOUNT");
        Assert(burnDto.AmountB >= amountAMin, "OpdexV1: INSUFFICIENT_B_AMOUNT");
        
        return new RemoveLiquidityResponseModel
        {
            AmountA = burnDto.AmountA,
            AmountB = burnDto.AmountB
        };
    }

    public RemoveLiquidityCRSResponseModel RemoveLiquidityCRS(Address token, ulong liquidity, ulong amountTokenMin, ulong amountCRSMin, Address to, ulong deadline)
    {
        var tokenAmounts = RemoveLiquidity(token, WCRS, liquidity, amountTokenMin, amountCRSMin, Address, deadline);

        // Todo: think about and validate that this will always be A
        Call(token, 0, "TransferTo", new object[] {to, tokenAmounts.AmountA});

        // Todo: There's permission issues here, the current implementation would suggest
        // that the router is withdrawing WCRS on its own, receiving the CRS balance,  
        // then transferring the CRS back to the caller.
        Call(WCRS, 0, "Withdraw", new object[] { tokenAmounts.AmountB });

        Transfer(to, tokenAmounts.AmountB);

        return new RemoveLiquidityCRSResponseModel
        {
            AmountToken = tokenAmounts.AmountA,
            AmountCRS = tokenAmounts.AmountB
        };
    }
    #endregion
    
    #region Swaps

    public void SwapExactTokensForTokens()
    {
        
    }

    public void SwapTokensForExactTokens()
    {
        
    }

    public void SwapExactCRSForTokens()
    {
        
    }

    public void SwapTokensForExactCRS()
    {
        
    }

    public void SwapExactTokensForCRS()
    {
        
    }

    public void SwapCRSForExactTokens()
    {
        
    }

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

    #region Models

    public struct AddLiquidityResponseModel
    {
        public ulong AmountA;
        public ulong AmountB;
        public ulong Liquidity;
    }
    
    public struct AddLiquidityCRSResponseModel
    {
        public ulong AmountToken;
        public ulong AmountCRS;
        public ulong Liquidity;
    }

    public struct RemoveLiquidityResponseModel
    {
        public ulong AmountA;
        public ulong AmountB;
    }
    
    public struct RemoveLiquidityCRSResponseModel
    {
        public ulong AmountToken;
        public ulong AmountCRS;
    }

    private struct BurnDto
    {
        public ulong AmountA;
        public ulong AmountB;
    }

    private struct ReservesDto
    {
        public ulong ReserveA;
        public ulong ReserveB;
    }
    
    private struct CalcLiquidityDto
    {
        public ulong AmountA;
        public ulong AmountB;
    }

    #endregion
}
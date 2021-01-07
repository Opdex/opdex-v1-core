using Stratis.SmartContracts;

[Deploy]
public class OpdexV1Controller : SmartContract
{
    public OpdexV1Controller(ISmartContractState smartContractState, Address feeToSetter, Address feeTo) : base(smartContractState)
    {
        FeeToSetter = feeToSetter;
        FeeTo = feeTo;
    }
    
    public Address GetPair(Address token) => PersistentState.GetAddress($"Pair:{token}");
    
    private void SetPair(Address token, Address contract) => PersistentState.SetAddress($"Pair:{token}", contract);

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

    public Address GetFeeTo() => FeeTo;
    
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
    
    /// <summary>
    /// Creates a new pair contract if one does not already exist.
    /// </summary>
    /// <param name="token">The SRC token used to create the pairing</param>
    /// <returns>Pair contract address</returns>
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
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="token"></param>
    /// <param name="amountTokenDesired"></param>
    /// <param name="amountCrsMin"></param>
    /// <param name="amountTokenMin"></param>
    /// <param name="to"></param>
    /// <param name="deadline"></param>
    /// <returns></returns>
    public AddLiquidityResponseModel AddLiquidity(Address token, ulong amountTokenDesired, ulong amountCrsMin, ulong amountTokenMin, Address to, ulong deadline)
    {
        var liquidityDto = CalcLiquidity(token, Message.Value, amountTokenDesired, amountCrsMin, amountTokenMin);
        SafeTransferFrom(token, Message.Sender, liquidityDto.Pair, liquidityDto.AmountToken);
        SafeTransfer(liquidityDto.Pair, liquidityDto.AmountCrs);
        var liquidityResponse = Call(liquidityDto.Pair, 0, "Mint", new object[] {to});
        SafeTransfer(Message.Sender, Message.Value - liquidityDto.AmountCrs);
        return new AddLiquidityResponseModel { AmountCrs = liquidityDto.AmountCrs, 
            AmountToken = liquidityDto.AmountToken, Liquidity = (ulong)liquidityResponse.ReturnValue };
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="token"></param>
    /// <param name="liquidity"></param>
    /// <param name="amountCrsMin"></param>
    /// <param name="amountTokenMin"></param>
    /// <param name="to"></param>
    /// <param name="deadline"></param>
    /// <returns></returns>
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
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="token"></param>
    /// <param name="amountCrsDesired"></param>
    /// <param name="amountTokenDesired"></param>
    /// <param name="amountCrsMin"></param>
    /// <param name="amountTokenMin"></param>
    /// <returns></returns>
    private CalcLiquidityModel CalcLiquidity(Address token, ulong amountCrsDesired, ulong amountTokenDesired, ulong amountCrsMin, ulong amountTokenMin)
    {
        ulong[] reserves;
        var pair = GetPair(token);
        if (pair != Address.Zero)
        {
            reserves = GetReserves(pair);
        }
        else
        {
            pair = CreatePair(token);
            reserves = new [] { 0ul, 0ul };
        }
        ulong amountCrs;
        ulong amountToken;
        if (reserves[0] == 0 && reserves[1] == 0)
        {
            amountCrs = amountCrsDesired;
            amountToken = amountTokenDesired;
        }
        else
        {
            var amountTokenOptimal = GetLiquidityQuote(amountCrsDesired, reserves[0], reserves[1]);
            if (amountTokenOptimal <= amountTokenDesired)
            {
                Assert(amountTokenOptimal >= amountTokenMin, "OpdexV1: INSUFFICIENT_B_AMOUNT");
                amountCrs = amountCrsDesired;
                amountToken = amountTokenOptimal;
            }
            else
            {
                var amountCrsOptimal = GetLiquidityQuote(amountTokenDesired, reserves[1], reserves[0]);
                Assert(amountCrsOptimal <= amountCrsDesired && amountCrsOptimal >= amountCrsMin, "OpdexV1: INSUFFICIENT_A_AMOUNT");
                amountCrs = amountCrsOptimal;
                amountToken = amountTokenDesired;
            }
        }
        return new CalcLiquidityModel { AmountCrs = amountCrs, AmountToken = amountToken, Pair = pair };
    }
    
    #endregion
    
    #region Swaps

    /// <summary>
    /// Equivalent to a CRS sell (e.g. Sell exactly 1 CRS for about 10 OPD)
    /// </summary>
    /// <param name="amountTokenOutMin"></param>
    /// <param name="token"></param>
    /// <param name="to"></param>
    /// <param name="deadline"></param>
    public void SwapExactCRSForTokens(ulong amountTokenOutMin, Address token, Address to, ulong deadline)
    {
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountOut = GetAmountOut(Message.Value, reserves[0], reserves[1]);
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
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountIn = GetAmountIn(amountCrsOut, reserves[1], reserves[0]);
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
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountOut = GetAmountOut(amountTokenIn, reserves[1], reserves[0]);
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
        var pair = GetValidatedPair(token);
        var reserves = GetReserves(pair);
        var amountIn = GetAmountIn(amountTokenOut, reserves[0], reserves[1]);
        Assert(amountIn <= Message.Value, "OpdexV1: EXCESSIVE_INPUT_AMOUNT");
        var change = Message.Value - amountIn;
        SafeTransfer(pair, amountIn);
        Swap(0, amountTokenOut, pair, to);
        SafeTransfer(Message.Sender, change);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="amountCrsOut"></param>
    /// <param name="amountTokenOut"></param>
    /// <param name="pair"></param>
    /// <param name="to"></param>
    private void Swap(ulong amountCrsOut, ulong amountTokenOut, Address pair, Address to)
    {
        var response = Call(pair, 0, "Swap", new object[] {amountCrsOut, amountTokenOut, to});
        Assert(response.Success, "OpdexV1: INVALID_SWAP_ATTEMPT");
    }
    
    #endregion
    
    #region Public Helpers
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="amountA"></param>
    /// <param name="reserveA"></param>
    /// <param name="reserveB"></param>
    /// <returns></returns>
    public ulong GetLiquidityQuote(ulong amountA, ulong reserveA, ulong reserveB)
    {
        Assert(amountA > 0, "OpdexV1: INSUFFICIENT_AMOUNT");
        Assert(reserveA > 0 && reserveB > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        return checked(amountA * reserveB) / reserveA;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="amountIn"></param>
    /// <param name="reserveIn"></param>
    /// <param name="reserveOut"></param>
    /// <returns></returns>
    public ulong GetAmountOut(ulong amountIn, ulong reserveIn, ulong reserveOut)
    {
        Assert(amountIn > 0, "OpdexV1: INSUFFICIENT_INPUT_AMOUNT");
        Assert(reserveIn > 0 && reserveOut > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        var amountInWithFee = checked(amountIn * 997);
        var numerator = checked(amountInWithFee * reserveOut);
        var denominator = checked(checked(reserveIn * 1000) + amountInWithFee);
        return numerator / denominator;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="amountOut"></param>
    /// <param name="reserveIn"></param>
    /// <param name="reserveOut"></param>
    /// <returns></returns>
    public ulong GetAmountIn(ulong amountOut, ulong reserveIn, ulong reserveOut)
    {
        Assert(amountOut > 0, "OpdexV1: INSUFFICIENT_OUTPUT_AMOUNT");
        Assert(reserveIn > 0 && reserveOut > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        var numerator = checked(checked(reserveIn * amountOut) * 1000);
        var denominator = checked(checked(reserveOut - amountOut) * 997);
        return checked((numerator / denominator) + 1);
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
        if (amount == 0) return; 
        var result = Transfer(to, amount);
        Assert(result.Success, "OpdexV1: INVALID_TRANSFER");
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

    #endregion
}

public class OpdexV1Pair : SmartContract
{
    // Todo: 1000 default - 10 for testing with ulong overflow bugs
    private const ulong MinimumLiquidity = 10;
    public OpdexV1Pair(ISmartContractState smartContractState, Address token) : base(smartContractState)
    {
        Factory = Message.Sender;
        Token = token;
    }

    #region Properties
    
    public Address Factory
    {
        get => PersistentState.GetAddress(nameof(Factory));
        private set => PersistentState.SetAddress(nameof(Factory), value);
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
    
    #endregion
    
    #region Liquidity Pool Tokens
    
    public ulong GetBalance(Address address) => PersistentState.GetUInt64($"Balance:{address}");
    
    private void SetBalance(Address address, ulong value) => PersistentState.SetUInt64($"Balance:{address}", value);
    
    public ulong GetAllowance(Address owner, Address spender) => PersistentState.GetUInt64($"Allowance:{owner}:{spender}");
    
    private void SetAllowance(Address owner, Address spender, ulong value) => PersistentState.SetUInt64($"Allowance:{owner}:{spender}", value);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="to"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TransferTo(Address to, ulong value) => TransferExecute(Message.Sender, to, value);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TransferFrom(Address from, Address to, ulong value)
    {
        var allowance = GetAllowance(from, Message.Sender);
        if (allowance > 0) SetAllowance(from, Message.Sender, checked(allowance - value));
        return TransferExecute(from, to, value);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="spender"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool Approve(Address spender, ulong value)
    {
        SetAllowance(Message.Sender, spender, value);
        Log(new ApprovalEvent {Owner = Message.Sender, Spender = spender, Value = value});
        return true;
    }
    
    #endregion
    
    #region Core

    /// <summary>
    /// Mints new tokens based on differences in reserves and balances
    /// </summary>
    /// <param name="to">The address to transfer the minted tokens to</param>
    /// <returns>Number of liquidity tokens minted</returns>
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
            liquidity = checked(Sqrt(checked(amountCrs * TotalSupply)) - MinimumLiquidity);
            MintExecute(Address.Zero, MinimumLiquidity);
        }
        else
        {
            var amountCrsLiquidity = checked(amountCrs * TotalSupply) / ReserveCrs;
            var amountTokenLiquidity = checked(amountToken * TotalSupply) / ReserveToken;
            liquidity = amountCrsLiquidity > amountTokenLiquidity ? amountTokenLiquidity : amountCrsLiquidity;
        }
        Assert(liquidity > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY");
        MintFee(reserves[0], reserves[1]);
        MintExecute(to, liquidity);
        Update(balanceCrs, balanceToken);
        Log(new MintEvent { AmountCrs = amountCrs, AmountToken = amountToken, Sender = Message.Sender });
        return liquidity;
    }

    /// <summary>
    /// Burns pool tokens when removing liquidity
    /// </summary>
    /// <param name="to">The address to transfer CRS/SRC tokens to</param>
    /// <returns></returns>
    public ulong[] Burn(Address to)
    {
        var reserves = GetReserves();
        var token = Token;
        var balanceCrs = Balance;
        var balanceToken = GetSrcBalance(token, Address);
        var liquidity = GetBalance(Address);
        var totalSupply = TotalSupply;
        var amountCrs = checked(liquidity * balanceCrs) / totalSupply;
        var amountToken = checked(liquidity * balanceToken) / totalSupply;
        Assert(amountCrs > 0 && amountToken > 0, "OpdexV1: INSUFFICIENT_LIQUIDITY_BURNED");
        MintFee(reserves[0], reserves[1]);
        BurnExecute(Address, liquidity);
        SafeTransfer(to, amountCrs);
        SafeTransferTo(token, to, amountToken);
        balanceCrs = Balance;
        balanceToken = GetSrcBalance(token, Address);
        Update(balanceCrs, balanceToken);
        KLast = checked(checked(reserves[0] - balanceCrs) * checked(reserves[1] - balanceToken));
        Log(new BurnEvent { Sender = Message.Sender, To = to, AmountCrs = amountCrs, AmountToken = amountToken });
        return new [] {amountCrs, amountToken};
    }

    /// <summary>
    /// Swaps tokens
    /// </summary>
    /// <param name="amountCrsOut"></param>
    /// <param name="amountTokenOut"></param>
    /// <param name="to"></param>
    public void Swap(ulong amountCrsOut, ulong amountTokenOut, Address to)
    {
        Assert(amountCrsOut > 0 || amountTokenOut > 0, "OpdexV1: INVALID_OUTPUT_AMOUNT");
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
        Assert(checked(balanceCrsAdjusted * balanceTokenAdjusted) >= checked(checked(reserves[0] * reserves[1]) * 1_000_000));
        Update(balanceCrs, balanceToken);
        Log(new SwapEvent { AmountCrsIn = amountCrsIn, AmountCrsOut = amountCrsOut, 
            AmountTokenIn = amountTokenIn, AmountTokenOut = amountTokenOut, Sender = Message.Sender, To = to });
    }

    /// <summary>
    /// Forces this contracts balances to match reserves
    /// </summary>
    /// <param name="to">The address to send the difference to</param>
    public void Skim(Address to)
    {
        var token = Token;
        var balanceToken = checked(GetSrcBalance(token, Address) - ReserveToken);
        var balanceCrs = checked(Balance - ReserveCrs);
        SafeTransfer(to, balanceToken);
        SafeTransferTo(token, to, balanceCrs);
        // Todo: Should this log an event?
    }  
    
    /// <summary>
    /// Forces the reserves amounts to match this contracts balances
    /// </summary>
    public void Sync() => Update(Balance, GetSrcBalance(Token, Address));
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public ulong[] GetReserves() => new [] { ReserveCrs, ReserveToken };
    
    #endregion

    #region Private Methods

    /// <summary>
    /// 
    /// </summary>
    /// <param name="balanceCrs"></param>
    /// <param name="balanceToken"></param>
    private void Update(ulong balanceCrs, ulong balanceToken)
    {
        ReserveCrs = balanceCrs;
        ReserveToken = balanceToken;
        Log(new SyncEvent { ReserveCrs = balanceCrs, ReserveToken = balanceToken });
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="reserveCrs"></param>
    /// <param name="reserveToken"></param>
    private void MintFee(ulong reserveCrs, ulong reserveToken)
    {
        var kLast = KLast;
        if (kLast == 0) return;
        var rootK = checked(Sqrt(reserveCrs) * reserveToken);
        var rootKLast = Sqrt(kLast);
        if (rootK <= rootKLast) return;
        var numerator = checked(TotalSupply * checked(rootK - rootKLast));
        var denominator = checked(checked(rootK * 5) + rootKLast);
        var liquidity = numerator / denominator;
        if (liquidity == 0) return;
        var feeToResponse = Call(Factory, 0, "GetFeeTo");
        var feeTo = (Address)feeToResponse.ReturnValue;
        Assert(feeToResponse.Success && feeTo != Address.Zero, "OpdexV1: INVALID_FEE_TO_ADDRESS");
        MintExecute(feeTo, liquidity);
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="to"></param>
    /// <param name="value"></param>
    private void MintExecute(Address to, ulong value)
    {
        var balance = GetBalance(to);
        TotalSupply = checked(TotalSupply + value);
        SetBalance(to, checked(balance + value));
        Log(new TransferEvent { From = Address.Zero, To = to, Value = value });
    }

    /// <summary>
    /// Gets the token balance of an address and validates the response
    /// </summary>
    /// <param name="token">The src token contract address</param>
    /// <param name="owner">The address to get the balance for</param>
    /// <returns>ulong balance</returns>
    private ulong GetSrcBalance(Address token, Address owner)
    {
        var balanceResponse = Call(token, 0, "GetBalance", new object[] {owner});
        Assert(balanceResponse.Success, "OpdexV1: INVALID_BALANCE");
        return (ulong)balanceResponse.ReturnValue;
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="from"></param>
    /// <param name="value"></param>
    private void BurnExecute(Address from, ulong value)
    {
        var balance = GetBalance(from);
        SetBalance(from, checked(balance - value));
        TotalSupply = checked(TotalSupply - value);
        Log(new TransferEvent { From = from, To = Address.Zero, Value = value });
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    private bool TransferExecute(Address from, Address to, ulong value)
    {
        var fromBalance = GetBalance(from);
        SetBalance(from, checked(fromBalance - value));
        var toBalance = GetBalance(to);
        SetBalance(to, checked(toBalance + value));
        Log(new TransferEvent {From = from, To = to, Value = value});
        return true;
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
        if (amount == 0) return;
        var result = Call(token, 0, "TransferTo", new object[] {to, amount});
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_FROM");
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="y"></param>
    /// <returns></returns>
    private static ulong Sqrt(ulong y)
    {
        ulong z = 0;
        if (y > 3) 
        {
            z = y;
            var x = y / 2 + 1;
            while (x < z) 
            {
                z = x;
                x = (y / x + x) / 2;
            }
        } 
        else if (y != 0) {
            z = 1;
        }
        return z;
    }
    
    #endregion

    #region Models

    public struct SyncEvent
    {
        public ulong ReserveCrs;
        public ulong ReserveToken;
    }
    
    public struct MintEvent
    {
        [Index] public Address Sender;
        public ulong AmountCrs;
        public ulong AmountToken;
    }
    
    public struct BurnEvent
    {
        [Index] public Address Sender;
        [Index] public Address To;
        public ulong AmountCrs;
        public ulong AmountToken;
    }
    
    public struct SwapEvent
    {
        [Index] public Address Sender;
        [Index] public Address To;
        public ulong AmountCrsIn;
        public ulong AmountTokenIn;
        public ulong AmountCrsOut;
        public ulong AmountTokenOut;
    }
    
    public struct ApprovalEvent
    {
        [Index] public Address Owner;
        [Index] public Address Spender;
        public ulong Value;
    }

    public struct TransferEvent
    {
        [Index] public Address From;
        [Index] public Address To;
        public ulong Value;
    }

    #endregion
}
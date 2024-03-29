using Stratis.SmartContracts;

public interface IOpdexRouter
{
    /// <summary>
    /// The address of the market the router is intended for.
    /// </summary>
    Address Market { get; }

    /// <summary>
    /// The transaction fee per swap transaction. Ranging from 0-10 equal to 0-1%.
    /// </summary>
    uint TransactionFee { get; }

    /// <summary>
    /// Flag indicating if liquidity providers should be authorized.
    /// </summary>
    bool AuthProviders { get; }

    /// <summary>
    /// Flag indicating if traders should be authorized.
    /// </summary>
    bool AuthTraders { get; }

    /// <summary>
    /// Retrieve a pool that has been previously fetched from the market and stored in the router contract.
    /// </summary>
    /// <param name="token">The address of the SRC token used to lookup the pool.</param>
    /// <returns>The address of the stored liquidity pool.</returns>
    Address GetPool(Address token);

    /// <summary>
    /// Allows users to provide liquidity to a pool, provided amounts must match the same ratio as the pool's current reserves.
    /// SRC20 tokens used for providing liquidity must have previously approved an allowance for this contract to spend.
    /// </summary>
    /// <param name="token">The SRC token address to lookup its pool by.</param>
    /// <param name="amountSrcDesired">The desired amount of SRC tokens to deposit.</param>
    /// <param name="amountCrsMin">The minimum amount of CRS tokens to deposit.</param>
    /// <param name="amountSrcMin">The minimum amount of SRC tokens to deposit.</param>
    /// <param name="to">The address to deposit the liquidity pool tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    /// <returns>The final amounts used to provide liquidity. [amountCrsIn, amountSrcIn, amountLptOut]</returns>
    UInt256[] AddLiquidity(Address token, UInt256 amountSrcDesired, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline);

    /// <summary>
    /// Remove liquidity from a specified pool. Liquidity Pool tokens being removed and burned must
    /// have previously approved the market contract with the desired burn amount.
    /// </summary>
    /// <param name="token">The SRC token address to lookup its pool by.</param>
    /// <param name="liquidity">The amount of liquidity pool tokens to remove.</param>
    /// <param name="amountCrsMin">The minimum amount of CRS tokens acceptable to receive.</param>
    /// <param name="amountSrcMin">The minimum amount of SRC tokens acceptable to receive.</param>
    /// <param name="to">The address to send the CRS and SRC tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    /// <returns>The amounts of received reserve tokens. [amountCrsOut, amountSrcOut]</returns>
    UInt256[] RemoveLiquidity(Address token, UInt256 liquidity, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline);

    /// <summary>
    /// Swaps an exact amount of CRS tokens for a set minimum amount of SRC tokens.
    /// </summary>
    /// <param name="amountSrcOutMin">The minimum amount of SRC tokens acceptable to receive.</param>
    /// <param name="token">The SRC token address to lookup its pool by.</param>
    /// <param name="to">The address to send the received SRC tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    /// <returns>The final amount of SRC tokens received.</returns>
    UInt256 SwapExactCrsForSrc(UInt256 amountSrcOutMin, Address token, Address to, ulong deadline);

    /// <summary>
    /// Swaps a maximum amount of SRC tokens for an exact amount of CRS tokens.
    /// SRC tokens must have previously approved the market contract with the desired amount.
    /// </summary>
    /// <param name="amountCrsOut">The exact amount of CRS tokens to receive.</param>
    /// <param name="amountSrcInMax">The maximum amount of SRC tokens to swap.</param>
    /// <param name="token">The SRC token address to lookup its pool by.</param>
    /// <param name="to">The address to send the CRS tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    /// <returns>The final amount of CRS tokens swapped.</returns>
    UInt256 SwapSrcForExactCrs(ulong amountCrsOut, UInt256 amountSrcInMax, Address token, Address to, ulong deadline);

    /// <summary>
    /// Swaps an exact amount of SRC tokes for a minimum amount of CRS tokens. Swapped SRC tokens must have
    /// previously approved the market contract with the desired amount.
    /// </summary>
    /// <param name="amountSrcIn">The exact amount of SRC tokens to swap.</param>
    /// <param name="amountCrsOutMin">The minimum amount of CRS tokens to receive.</param>
    /// <param name="token">The SRC token address to lookup its pool by.</param>
    /// <param name="to">The address to send the CRS tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    /// <returns>The final amount of CRS tokens received.</returns>
    ulong SwapExactSrcForCrs(UInt256 amountSrcIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline);

    /// <summary>
    /// Swaps a maximum amount of CRS tokens for an exact amount of SRC tokens, overpayment is sent back to the transaction sender.
    /// </summary>
    /// <param name="amountSrcOut">The exact amount of SRC tokens to receive.</param>
    /// <param name="token">The SRC token address to lookup its pool by.</param>
    /// <param name="to">The address to send the SRC tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    /// <returns>The final amount of CRS tokens swapped.</returns>
    ulong SwapCrsForExactSrc(UInt256 amountSrcOut, Address token, Address to, ulong deadline);

    /// <summary>
    /// Swaps a maximum amount of SRC tokens for an exact amount of SRC tokens. SRC tokens being swapped must
    /// have previously approved the market contract with the desired amount.
    /// </summary>
    /// <remarks>
    /// SRC to SRC token swaps hop between two pools which incurs double the market's transaction
    /// fees but is done in a single transaction. Input SRC tokens are swapped for CRS tokens which
    /// are then swapped for the desired SRC tokens.
    /// </remarks>
    /// <param name="amountSrcInMax">The maximum amount of SRC tokens to swap.</param>
    /// <param name="tokenIn">The address of the SRC token being swapped.</param>
    /// <param name="amountSrcOut">The exact amount of SRC tokens to receive.</param>
    /// <param name="tokenOut">The address of the SRC token being received.</param>
    /// <param name="to">The address to send the SRC tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    /// <returns>The final amount of SRC tokens swapped.</returns>
    UInt256 SwapSrcForExactSrc(UInt256 amountSrcInMax, Address tokenIn, UInt256 amountSrcOut, Address tokenOut, Address to, ulong deadline);

    /// <summary>
    /// Swaps an exact amount of SRC tokens for a minimum amount of SRC tokens. SRC tokens being swapped must
    /// have previously approved the market contract with the desired amount.
    /// </summary>
    /// <remarks>
    /// SRC to SRC token swaps hop between two pools which incurs double the market's transaction
    /// fees but is done in a single transaction. Input SRC tokens are swapped for CRS tokens which
    /// are then swapped for the desired SRC tokens.
    /// </remarks>
    /// <param name="amountSrcIn">The amount of SRC tokens to swap.</param>
    /// <param name="tokenIn">The address of the SRC token being swapped.</param>
    /// <param name="amountSrcOutMin">The minimum amount of SRC tokens to receive.</param>
    /// <param name="tokenOut">The address of the SRC token being received.</param>
    /// <param name="to">The address to send the SRC tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    /// <returns>The final amount of SRC tokens received.</returns>
    UInt256 SwapExactSrcForSrc(UInt256 amountSrcIn, Address tokenIn, UInt256 amountSrcOutMin, Address tokenOut, Address to, ulong deadline);

    /// <summary>
    /// Calculates the value of amountB to be deposited with amountA to a pool based on the pool's current reserves.
    /// </summary>
    /// <param name="amountA">The amount of TokenA tokens to provide.</param>
    /// <param name="reserveA">The pool's reserve amount of TokenA.</param>
    /// <param name="reserveB">The pool's reserve of the TokenB.</param>
    /// <returns>The amount of necessary tokens to provide for TokenB.</returns>
    UInt256 GetLiquidityQuote(UInt256 amountA, UInt256 reserveA, UInt256 reserveB);

    /// <summary>
    /// Calculate the amount returned after transaction fees based on the token input amount and the pool's reserves.
    /// Used for CRS-SRC or SRC-CRS single pool transactions.
    /// </summary>
    /// <param name="amountIn">The amount of the token to deposit.</param>
    /// <param name="reserveIn">The pool's reserve amount of the input token type.</param>
    /// <param name="reserveOut">The pool's reserve amount of the output token type.</param>
    /// <returns>Number of tokens to receive.</returns>
    UInt256 GetAmountOut(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut);

    /// <summary>
    /// Calculates the necessary deposit amount, including transaction fees, based on the amount to receive and the pool's reserves.
    /// Used for CRS-SRC or SRC-CRS single pool transactions.
    /// </summary>
    /// <param name="amountOut">The amount of tokens to receive.</param>
    /// <param name="reserveIn">The pool's reserve amount of the input token type.</param>
    /// <param name="reserveOut">The pool's reserve amount of the output token type.</param>
    /// <returns>Number of tokens to deposit.</returns>
    UInt256 GetAmountIn(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut);

    /// <summary>
    /// Calculates the necessary SRC deposit amount, including transaction fees, based on the amount to receive and the pool's reserves.
    /// Used for SRC-SRC multi-pool transactions.
    /// </summary>
    /// <param name="tokenOutAmount">The amount of SRC tokens to receive.</param>
    /// <param name="tokenOutReserveCrs">The pool's CRS reserve amount of the output token type.</param>
    /// <param name="tokenOutReserveSrc">The pool's SRC reserve amount of the output token type.</param>
    /// <param name="tokenInReserveCrs">The pool's CRS reserve amount of the input token type.</param>
    /// <param name="tokenInReserveSrc">The pool's SRC reserve amount of the input token type.</param>
    /// <returns>The number of SRC tokens needed to be input for the desired output.</returns>
    UInt256 GetAmountIn(UInt256 tokenOutAmount, UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc, UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc);

    /// <summary>
    /// Calculates the amount of SRC tokens returned after transaction fees based on the token input amount and the pool's reserves.
    /// Used for SRC-SRC multi-pool transactions.
    /// </summary>
    /// <param name="tokenInAmount">The amount of SRC tokens necessary to deposit.</param>
    /// <param name="tokenInReserveCrs">The pool's CRS reserve amount of the input token type.</param>
    /// <param name="tokenInReserveSrc">The pool's SRC reserve amount of the input token type.</param>
    /// <param name="tokenOutReserveCrs">The pool's CRS reserve amount of the output token type.</param>
    /// <param name="tokenOutReserveSrc">The pool's SRC reserve amount of the output token type.</param>
    /// <returns>The number of SRC tokens to be received with the desired input amount.</returns>
    UInt256 GetAmountOut(UInt256 tokenInAmount, UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc, UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc);
}
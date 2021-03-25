using Stratis.SmartContracts;

public interface IOpdexController
{
    /// <summary>
    /// The address of the staking token.
    /// </summary>
    Address StakeToken { get; }

    /// <summary>
    /// Retrieve a pool's contract address by the SRC token associated.
    /// </summary>
    /// <param name="token">The address of the SRC token.</param>
    /// <returns>Address of the pool</returns>
    Address GetPool(Address token);
    
    /// <summary>
    /// Create a liquidity pool for the provided token.
    /// </summary>
    /// <param name="token">The address of the SRC token.</param>
    /// <returns>Address of the pool</returns>
    Address CreatePool(Address token);
    
    /// <summary>
    /// Provides liquidity to a specified pool. SRC tokens being provided must have previously
    /// approved the controller contract with the desired allowance.
    /// </summary>
    /// <param name="token">The SRC token address to lookup its pool by.</param>
    /// <param name="amountSrcDesired">The wishful amount of SRC tokens to deposit.</param>
    /// <param name="amountCrsMin">The minimum amount of CRS tokens to deposit.</param>
    /// <param name="amountSrcMin">The minimum amount of SRC tokens to deposit.</param>
    /// <param name="to">The address to deposit the liquidity pool tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    /// <returns></returns>
    object[] AddLiquidity(Address token, UInt256 amountSrcDesired, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline);
    
    /// <summary>
    /// Remove liquidity from a specified pool. Liquidity Pool tokens being removed and burned must
    /// have previously approved the controller contract with the desired burn amount.
    /// </summary>
    /// <param name="token">The SRC token address to lookup its pool by.</param>
    /// <param name="liquidity">The amount of liquidity pool tokens to remove.</param>
    /// <param name="amountCrsMin">The minimum amount of CRS tokens acceptable to receive.</param>
    /// <param name="amountSrcMin">The minimum amount of SRC tokens acceptable to receive.</param>
    /// <param name="to">The address to send the CRS and SRC tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    /// <returns></returns>
    object[] RemoveLiquidity(Address token, UInt256 liquidity, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline);
    
    /// <summary>
    /// Swaps an exact amount of CRS tokens for a set minimum amount of SRC tokens. 
    /// </summary>
    /// <param name="amountSrcOutMin">The minimum amount of SRC tokens acceptable.</param>
    /// <param name="token">The SRC token address to lookup its pool by.</param>
    /// <param name="to">The address to send the SRC tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    void SwapExactCrsForSrc(UInt256 amountSrcOutMin, Address token, Address to, ulong deadline);
    
    /// <summary>
    /// Swaps a maximum set amount of SRC tokens for an exact amount of CRS tokens. SRC tokens must have
    /// previously approved the controller contract with the desired amount.
    /// </summary>
    /// <param name="amountCrsOut">The exact amount of CRS tokens to receive.</param>
    /// <param name="amountSrcInMax">The maximum amount of SRC tokens to swap.</param>
    /// <param name="token">The SRC token address to lookup its pool by.</param>
    /// <param name="to">The address to send the CRS tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    void SwapSrcForExactCrs(ulong amountCrsOut, UInt256 amountSrcInMax, Address token, Address to, ulong deadline);
    
    /// <summary>
    /// Swaps an exact amount of SRC tokes for a minimum amount of CRS tokens. Swapped SRC tokens must have
    /// previously approved the controller contract with the desired amount.
    /// </summary>
    /// <param name="amountSrcIn">The exact amount of SRC tokens to swap.</param>
    /// <param name="amountCrsOutMin">The minimum amount of CRS tokens to receive.</param>
    /// <param name="token">The SRC token address to lookup its pool by.</param>
    /// <param name="to">The address to send the CRS tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    void SwapExactSrcForCrs(UInt256 amountSrcIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline);
    
    /// <summary>
    /// Swaps CRS tokens for an exact amount of SRC tokens.
    /// </summary>
    /// <param name="amountSrcOut">The exact amount of SRC tokens to receive.</param>
    /// <param name="token">The SRC token address to lookup its pool by.</param>
    /// <param name="to">The address to send the SRC tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    void SwapCrsForExactSrc(UInt256 amountSrcOut, Address token, Address to, ulong deadline);
    
    /// <summary>
    /// Swaps a maximum amount of SRC tokens for an exact amount of SRC tokens. SRC tokens being swapped must
    /// have previously approved the controller contract with the desired amount.
    /// </summary>
    /// <param name="amountSrcInMax">The maximum amount of SRC tokens to swap.</param>
    /// <param name="tokenIn">The address of the SRC token being swapped.</param>
    /// <param name="amountSrcOut">The exact amount of SRC tokens to receive.</param>
    /// <param name="tokenOut">The address of the SRC token being received.</param>
    /// <param name="to">The address to send the SRC tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    void SwapSrcForExactSrc(UInt256 amountSrcInMax, Address tokenIn, UInt256 amountSrcOut, Address tokenOut, Address to, ulong deadline);
    
    /// <summary>
    /// Swaps an exact amount of SRC tokens for a minimum amount of SRC tokens. SRC tokens being swapped must
    /// have previously approved the controller contract with the desired amount.
    /// </summary>
    /// <param name="amountSrcIn">The amount of SRC tokens to swap.</param>
    /// <param name="tokenIn">The address of the SRC token being swapped.</param>
    /// <param name="amountSrcOutMin">The minimum amount of SRC tokens to receive.</param>
    /// <param name="tokenOut">The address of the SRC token being received.</param>
    /// <param name="to">The address to send the SRC tokens to.</param>
    /// <param name="deadline">Block number deadline to execute the transaction by.</param>
    void SwapExactSrcForSrc(UInt256 amountSrcIn, Address tokenIn, UInt256 amountSrcOutMin, Address tokenOut, Address to, ulong deadline);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="amountA"></param>
    /// <param name="reserveA"></param>
    /// <param name="reserveB"></param>
    /// <returns></returns>
    UInt256 GetLiquidityQuote(UInt256 amountA, UInt256 reserveA, UInt256 reserveB);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="amountIn"></param>
    /// <param name="reserveIn"></param>
    /// <param name="reserveOut"></param>
    /// <returns></returns>
    UInt256 GetAmountOut(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="amountOut"></param>
    /// <param name="reserveIn"></param>
    /// <param name="reserveOut"></param>
    /// <returns></returns>
    UInt256 GetAmountIn(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="amountSrcOut"></param>
    /// <param name="srcOutReserveCrs"></param>
    /// <param name="srcOutReserveSrc"></param>
    /// <param name="crsInReserveSrc"></param>
    /// <param name="crsInReserveCrs"></param>
    /// <returns></returns>
    UInt256[] GetAmountIn(UInt256 amountSrcOut, UInt256 srcOutReserveCrs, UInt256 srcOutReserveSrc, UInt256 crsInReserveSrc, UInt256 crsInReserveCrs);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="amountSrcIn"></param>
    /// <param name="srcInReserveSrc"></param>
    /// <param name="srcInReserveCrs"></param>
    /// <param name="crsOutReserveCrs"></param>
    /// <param name="crsOutReserveSrc"></param>
    /// <returns></returns>
    UInt256[] GetAmountOut(UInt256 amountSrcIn, UInt256 srcInReserveSrc, UInt256 srcInReserveCrs, UInt256 crsOutReserveCrs, UInt256 crsOutReserveSrc);
}
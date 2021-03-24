using Stratis.SmartContracts;

public interface IOpdexController
{
    Address StakeToken { get; }

    Address GetPool(Address token);
    Address CreatePool(Address token);
    object[] AddLiquidity(Address token, UInt256 amountSrcDesired, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline);
    object[] RemoveLiquidity(Address token, UInt256 liquidity, ulong amountCrsMin, UInt256 amountSrcMin, Address to, ulong deadline);
    void SwapExactCrsForSrc(UInt256 amountSrcOutMin, Address token, Address to, ulong deadline);
    void SwapSrcForExactCrs(ulong amountCrsOut, UInt256 amountSrcInMax, Address token, Address to, ulong deadline);
    void SwapExactSrcForCrs(UInt256 amountSrcIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline);
    void SwapCrsForExactSrc(UInt256 amountSrcOut, Address token, Address to, ulong deadline);
    void SwapSrcForExactSrc(UInt256 amountSrcInMax, Address tokenIn, UInt256 amountSrcOut, Address tokenOut, Address to, ulong deadline);
    void SwapExactSrcForSrc(UInt256 amountSrcIn, Address tokenIn, UInt256 amountSrcOutMin, Address tokenOut, Address to, ulong deadline);
    UInt256 GetLiquidityQuote(UInt256 amountA, UInt256 reserveA, UInt256 reserveB);
    UInt256 GetAmountOut(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut);
    UInt256 GetAmountIn(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut);
    UInt256[] GetAmountIn(UInt256 amountSrcOut, UInt256 srcOutReserveCrs, UInt256 srcOutReserveSrc, UInt256 crsInReserveSrc, UInt256 crsInReserveCrs);
    UInt256[] GetAmountOut(UInt256 amountSrcIn, UInt256 srcInReserveSrc, UInt256 srcInReserveCrs, UInt256 crsOutReserveCrs, UInt256 crsOutReserveSrc);
}
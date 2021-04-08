# Opdex Controller Contract

A smart contract that acts as an entry point to Opdex protocol for most pool related interactions. This contract is primarily responsible for the following items:
- Management of available pools
- Quoting swap and pooling related methods. 
- Validating and completing prerequisite actions prior to providing or swapping in a pool.

## Overview 

Traders will approve the controller contract for an allowance per token they want to swap, provide or remove liquidity for. 

This controller contract will use `TransferFrom` amongst other validations prior to calling a pool to swap, add, or remove liquidity.

This contract only stores Pool addresses looked up by SRC token address so new versions can easily be deployed and used as long as existing pools are set in new versions.

## Manage Pools

Pools are created and retrieved by the SRC token in the pool. When creating a pool, if it already exists, the existing address will be returned, else it will create a new pool.

### Get Pool
```C#
/// <summary>
/// Retrieve a pool's contract address by the SRC token associated.
/// </summary>
/// <param name="token">The address of the SRC token.</param>
/// <returns>Address of the pool</returns>
Address GetPool(Address token);
```

### Create Pool

```C#
/// <summary>
/// Create a liquidity pool for the provided token.
/// </summary>
/// <param name="token">The address of the SRC token.</param>
/// <returns>Address of the pool</returns>
Address CreatePool(Address token);
```

## Pooling Liquidity

Providing liquidity and removing liquidity from pools happens by first routing through this controller contract which performs
allowance checks, validations, and calls liquidity pools directly all in one transaction. 

### Add Liquidity

Allows users to provide liquidity to a pool, provided amounts must match the same ratio as the pool's current reserves.

```C#
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
```

### Remove Liquidity

Allows users to remove liquidity from a pool. Burns the pool's liquidity pool tokens in return, sends the user's share of the pool's reserves to the specified address.

```C#
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
```

### Get Liquidity Quote

Given the desired pool's current reserves and an input amount, calculate the amount of tokens needed to provide liquidity to a pool. 

```C#
/// <summary>
/// Calculate the necessary amount to provide of TokenB in a pool by the TokenA's desired amount and the pool's
/// current reserves.
/// </summary>
/// <param name="amountA">The amount of TokenA desired to provide.</param>
/// <param name="reserveA">The pool's reserve amount of TokenA's type.</param>
/// <param name="reserveB">The pool's reserve of the TokenB's type.</param>
/// <returns>Number of necessary tokens to provide.</returns>
UInt256 GetLiquidityQuote(UInt256 amountA, UInt256 reserveA, UInt256 reserveB);
```

## Exact Input Swaps

### Swap Exact CRS for SRC

```C#
/// <summary>
/// Swaps an exact amount of CRS tokens for a set minimum amount of SRC tokens. 
/// </summary>
/// <param name="amountSrcOutMin">The minimum amount of SRC tokens acceptable.</param>
/// <param name="token">The SRC token address to lookup its pool by.</param>
/// <param name="to">The address to send the SRC tokens to.</param>
/// <param name="deadline">Block number deadline to execute the transaction by.</param>
void SwapExactCrsForSrc(UInt256 amountSrcOutMin, Address token, Address to, ulong deadline);
```

### Swap Exact SRC for CRS

```C#
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
```

### Swap Exact SRC for SRC

```C#
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
```

### Get Output Amount

```C#
/// <summary>
/// Calculate the amount returned after transaction fees based on the token input amount and the pool's reserves.
/// Used for CRS-SRC or SRC-CRS single pool transactions.
/// </summary>
/// <param name="amountIn">The amount of the token to deposit.</param>
/// <param name="reserveIn">The pool's reserve amount of the input token type.</param>
/// <param name="reserveOut">The pool's reserve amount of the output token type.</param>
/// <returns>Number of tokens to receive</returns>
UInt256 GetAmountOut(UInt256 amountIn, UInt256 reserveIn, UInt256 reserveOut);
```

```C#
/// <summary>
/// Calculates the amount of SRC tokens returned after transaction fees based on the token input amount and the pool's reserves.
/// Used for SRC-SRC multi pool transactions.
/// </summary>
/// <param name="tokenInAmount">The amount of SRC tokens necessary to deposit.</param>
/// <param name="tokenInReserveCrs">The pool's CRS reserve amount of the input token type.</param>
/// <param name="tokenInReserveSrc">The pool's SRC reserve amount of the input token type.</param>
/// <param name="tokenOutReserveCrs">The pool's CRS reserve amount of the output token type.</param>
/// <param name="tokenOutReserveSrc">The pool's SRC reserve amount of the output token type.</param>
/// <returns>Number of SRC tokens to receive</returns>
UInt256 GetAmountOut(UInt256 tokenInAmount, UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc, UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc);
```

## Exact Output Swaps

### Swap CRS for Exact SRC

```C#
/// <summary>
/// Swaps CRS tokens for an exact amount of SRC tokens.
/// </summary>
/// <param name="amountSrcOut">The exact amount of SRC tokens to receive.</param>
/// <param name="token">The SRC token address to lookup its pool by.</param>
/// <param name="to">The address to send the SRC tokens to.</param>
/// <param name="deadline">Block number deadline to execute the transaction by.</param>
void SwapCrsForExactSrc(UInt256 amountSrcOut, Address token, Address to, ulong deadline);
```

### Swap SRC for Exact CRS

```C#
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
```

### Swap SRC for Exact SRC

```C#
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
```

### Get Input Amount

```C#
/// <summary>
/// Calculates the necessary deposit amount based on the amount to receive and the pool's reserves.
/// Used for CRS-SRC or SRC-CRS single pool transactions.
/// </summary>
/// <param name="amountOut">The amount of tokens to receive.</param>
/// <param name="reserveIn">The pool's reserve amount of the input token type.</param>
/// <param name="reserveOut">The pool's reserve amount of the output token type.</param>
/// <returns>Number of tokens to deposit</returns>
UInt256 GetAmountIn(UInt256 amountOut, UInt256 reserveIn, UInt256 reserveOut);
```

```C#
/// <summary>
/// Calculates the necessary SRC deposit amount based on the amount to receive and the pool's reserves.
/// Used for SRC-SRC multi pool transactions.
/// </summary>
/// <param name="tokenOutAmount">The amount of SRC tokens to receive.</param>
/// <param name="tokenOutReserveCrs">The pool's CRS reserve amount of the output token type.</param>
/// <param name="tokenOutReserveSrc">The pool's SRC reserve amount of the output token type.</param>
/// <param name="tokenInReserveCrs">The pool's CRS reserve amount of the input token type.</param>
/// <param name="tokenInReserveSrc">The pool's SRC reserve amount of the input token type.</param>
/// <returns>Number of SRC tokens necessary to deposit</returns>
UInt256 GetAmountIn(UInt256 tokenOutAmount, UInt256 tokenOutReserveCrs, UInt256 tokenOutReserveSrc, UInt256 tokenInReserveCrs, UInt256 tokenInReserveSrc);
```

---

Ported and adjusted based on https://github.com/Uniswap/uniswap-v2-periphery
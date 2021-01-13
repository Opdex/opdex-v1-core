# Opdex V1 Contracts

Two core contracts, a controller contract, and a child pair contract that can be created through the controller contract.

## Compile and Deploy

**Compile Version**

**Bytecode**

``` text
// To be filled
```

**Hash**

``` text
// To be filled
```

## Controller Contract

The main controller contract responsible for routing calls through pair contracts ensuring fully atomic swaps in a single transaction.

### Get Pair

```C#
/// <summary>
/// 
/// </summary>
/// <param name="token"></param>
Address GetPair(Address token);
```

### Get Fee To

```C#
/// <summary>
/// 
/// </summary>
Address GetFeeTo();
```

### Set Fee To

```C#
/// <summary>
/// 
/// </summary>
/// <param name="feeTo"></param>
void SetFeeTo(Address feeTo);
```

### Set Fee To Setter

```C#
/// <summary>
/// 
/// </summary>
/// <param name="feeToSetter"></param>
void SetFeeToSetter(Address feeToSetter);
```

### Create Pair

```C#
/// <summary>
/// Creates a new pair contract if one does not already exist.
/// </summary>
/// <param name="token">The SRC token used to create the pairing</param>
/// <returns>Pair contract address</returns>
Address CreatePair(Address token);
```

### Add Liquidity

```C#
/// <summary>
/// Add liquidity to a pool for the provided token to receive liquidity pool tokens
/// </summary>
/// <param name="token">The token address to find a pair for</param>
/// <param name="amountTokenDesired">The desired amount of SRC tokens to add</param>
/// <param name="amountCrsMin">The minimum amount of CRS tokens to add</param>
/// <param name="amountTokenMin">The minimum of SRC tokens to add</param>
/// <param name="to">The address to receive the liquidity pool tokens at</param>
/// <param name="deadline"></param>
/// <returns>Object containing the amount of SRC and CRS added to the pool and how many liquidity tokens were minted</returns>
AddLiquidityResponseModel AddLiquidity(Address token, ulong amountTokenDesired, ulong amountCrsMin, ulong amountTokenMin, Address to, ulong deadline);
```

### Remove Liquidity

```C#
/// <summary>
/// Removes liquidity from a pool for the provided token address, burns liquidity pool tokens
/// </summary>
/// <param name="token">The token address to find a pair for</param>
/// <param name="liquidity">The amount of liquidity to burn</param>
/// <param name="amountCrsMin">The minimum amount of CRS to receive for burning liquidity</param>
/// <param name="amountTokenMin">The minimum amount of SRC to receive for burning liquidity</param>
/// <param name="to">The address to send the CRS and SRC tokens to</param>
/// <param name="deadline"></param>
/// <returns>Object containing the amount of SRC and CRS removed from the pool</returns>
RemoveLiquidityResponseModel RemoveLiquidity(Address token, ulong liquidity, ulong amountCrsMin, ulong amountTokenMin, Address to, ulong deadline);
```

### Swap Exact CRS Tokens For SRC Tokens

```C#
/// <summary>
/// Equivalent to a CRS sell (e.g. Sell exactly 1 CRS for about 10 OPD)
/// </summary>
/// <param name="amountTokenOutMin"></param>
/// <param name="token"></param>
/// <param name="to"></param>
/// <param name="deadline"></param>
void SwapExactCRSForTokens(ulong amountTokenOutMin, Address token, Address to, ulong deadline);
```

### Swap SRC Tokens for Exact CRS Tokens

```C#
/// <summary>
/// Equivalent to a SRC sell (e.g. Sell about 10 OPD for exactly 1 CRS)
/// </summary>
/// <param name="amountCrsOut"></param>
/// <param name="amountTokenInMax"></param>
/// <param name="token"></param>
/// <param name="to"></param>
/// <param name="deadline"></param>
void SwapTokensForExactCRS(ulong amountCrsOut, ulong amountTokenInMax, Address token, Address to, ulong deadline);
```

### Swap Exact SRC Tokens for CRS Tokens

```C#
/// <summary>
/// Equivalent to a SRC sell (e.g. Sell exactly 10 OPD for about 1 CRS)
/// </summary>
/// <param name="amountTokenIn"></param>
/// <param name="amountCrsOutMin"></param>
/// <param name="token"></param>
/// <param name="to"></param>
/// <param name="deadline"></param>
void SwapExactTokensForCRS(ulong amountTokenIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline);
```

### Swap CRS Tokens for Exact SRC Tokens

```C#
/// <summary>
/// Equivalent to a CRS sell (e.g. Sell about 1 CRS for exactly 10 OPD)
/// </summary>
/// <param name="amountTokenOut"></param>
/// <param name="token"></param>
/// <param name="to"></param>
/// <param name="deadline"></param>
void SwapCRSForExactTokens(ulong amountTokenOut, Address token, Address to, ulong deadline);
```

### Get Liquidity Quote

```C#
/// <summary>
/// 
/// </summary>
/// <param name="amountA"></param>
/// <param name="reserveA"></param>
/// <param name="reserveB"></param>
/// <returns></returns>
ulong GetLiquidityQuote(ulong amountA, ulong reserveA, ulong reserveB);
```

### Get Amount Out

```C#
/// <summary>
/// 
/// </summary>
/// <param name="amountIn"></param>
/// <param name="reserveIn"></param>
/// <param name="reserveOut"></param>
/// <returns></returns>
ulong GetAmountOut(ulong amountIn, ulong reserveIn, ulong reserveOut);
```

### Get Amount In

```C#
/// <summary>
/// 
/// </summary>
/// <param name="amountOut"></param>
/// <param name="reserveIn"></param>
/// <param name="reserveOut"></param>
/// <returns></returns>
ulong GetAmountIn(ulong amountOut, ulong reserveIn, ulong reserveOut);
```

## Pair Contract

An individual contract per pairing of CRS/SRC tokens. Each pair includes it's own liquidity pool and liquidity pool tokens. This contract should not be called directly outside of being called from the controller contract but it is not restricted. Calling these contracts directly while mutating contract state can result in loss of funds due to arbitrage between transactions. This is intentional.

### Get Balance

```C#
ulong GetBalance(Address address);
```

### Allowance

```C#
ulong Allowance(Address owner, Address spender);
```

### Get Allowance

```C#
ulong GetAllowance(Address owner, Address spender);
```

### Transfer To

```C#
/// <summary>
/// 
/// </summary>
/// <param name="to"></param>
/// <param name="amount"></param>
/// <returns>Success as boolean</returns>
bool TransferTo(Address to, ulong amount) => TransferExecute(Message.Sender, to, amount);
```


### Transfer From

```C#
/// <summary>
/// 
/// </summary>
/// <param name="from"></param>
/// <param name="to"></param>
/// <param name="amount"></param>
/// <returns>Success as boolean</returns>
bool TransferFrom(Address from, Address to, ulong amount);
```

### Approve

```C#
bool Approve(Address spender, ulong currentAmount, ulong amount);

/// <summary>
/// 
/// </summary>
/// <param name="spender"></param>
/// <param name="amount"></param>
/// <returns></returns>
public bool Approve(Address spender, ulong amount);
```

### Mint

```C#
/// <summary>
/// Mints new tokens based on differences in reserves and balances
/// </summary>
/// <param name="to">The address to transfer the minted tokens to</param>
/// <returns>Number of liquidity tokens minted</returns>
ulong Mint(Address to);
```

### Burn

```C#
/// <summary>
/// Burns pool tokens when removing liquidity
/// </summary>
/// <param name="to">The address to transfer CRS/SRC tokens to</param>
/// <returns></returns>
ulong[] Burn(Address to);
```

### Swap

```C#
/// <summary>
/// Swaps tokens
/// </summary>
/// <param name="amountCrsOut"></param>
/// <param name="amountTokenOut"></param>
/// <param name="to"></param>
void Swap(ulong amountCrsOut, ulong amountTokenOut, Address to);
```

### Skim

```C#
/// <summary>
/// Forces this contracts balances to match reserves
/// </summary>
/// <param name="to">The address to send the difference to</param>
void Skim(Address to);
```

### Sync

```C#
/// <summary>
/// Forces the reserves amounts to match this contracts balances
/// </summary>
void Sync();
```

### Get Reserves

```C#
/// <summary>
/// 
/// </summary>
/// <returns></returns>
ulong[] GetReserves();
```

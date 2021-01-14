# Opdex V1 Controller Contract

The main contract responsible for routing calls through pair contracts ensuring fully atomic swaps in a single transaction.

___

## Public Methods

### Get Pair

```C#
/// <summary>
/// Get the contract address of the CRS/SRC pair for the given token.
/// </summary>
/// <params name="token">The contract address of the token to get the CRS/SRC pair for</params>
/// <returns>Address of the contract for the pair</returns>
Address GetPair(Address token);
```

### Get Fee To

```C#
/// <summary>
/// Retrieves the Fee To address that is set to receive the .05% transaction fee
/// </summary>
/// <returns>Address of the </returns>
Address GetFeeTo();
```

### Set Fee To

```C#
/// <summary>
/// Sets the Fee To address, requires senders address to be equal to the Fee To Setter address
/// </summary>
/// <param name="feeTo">The address to set as the new receiver</param>
void SetFeeTo(Address feeTo);
```

### Set Fee To Setter

```C#
/// <summary>
/// Sets the Fee To Setter address, requires senders address to be equal to the current Fee To Setter address
/// </summary>
/// <param name="feeToSetter">The new address to set as the Fee To Setter</param>
void SetFeeToSetter(Address feeToSetter);
```

### Create Pair

```C#
/// <summary>
/// Creates a new CRS/SRC pair contract if one does not already exist.
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
/// Calculates the token amount that will be transferred out based on the amount of tokens being sent in
/// and the provided liquidity pool reserves.
/// </summary>
/// <param name="amountIn">The amount of the token to be sent into a pair</param>
/// <param name="reserveIn">The reserves of the token that will be sent in (CRS reserves if amountIn is CRS, otherwise SRC reserves)</param>
/// <param name="reserveOut">The reserves of the other token in the pool (SRC reserves if amountIn is CRS, otherwise CRS reserves)</param>
/// <returns>Value of the amount of tokens that would be received</returns>
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

___

## Logged Events

Includes any possible types of events that are logged from calling any of the controller contract methods. Event logs queried individually through a Cirrus Full Node.

### Pair Created Event

Logged after a new CRS/SRC pair is created

```C#
public struct PairCreatedEvent
{
    public Address Token;
    public Address Pair;
}
```

___

## Response Models

### Add Liquidity Response Model

```C#
public struct AddLiquidityResponseModel
{
    public ulong AmountCrs;
    public ulong AmountToken;
    public ulong Liquidity;
}
```

### Remove Liquidity Response Model

```C#
public struct RemoveLiquidityResponseModel
{
    public ulong AmountCrs;
    public ulong AmountToken;
}
```

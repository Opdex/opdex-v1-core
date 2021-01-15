# Opdex V1 Controller Contract

The main contract responsible for routing calls through pair contracts ensuring fully atomic swaps in a single transaction.

## Public Methods

### Get Pair

Get the contract address of the CRS/SRC pair for the given token.

```C#
Address GetPair(Address token);
```

#### Parameters

**token** - The contract address of the token to get the CRS/SRC pair for

#### Returns

The address of the contract for the pair

___

### Get Fee To

Retrieves the Fee To address that is set to receive the .05% transaction fee

```C#
Address GetFeeTo();
```

#### Returns

Address that receives the .05% partial transaction fee<

___

### Set Fee To

Sets the Fee To address, requires senders address to be equal to the Fee To Setter address

```C#
void SetFeeTo(Address feeTo);
```

#### Parameters

**feeTo** - The address to set as the new receiver

___

### Set Fee To Setter

Sets the Fee To Setter address, requires senders address to be equal to the current Fee To Setter address

```C#
void SetFeeToSetter(Address feeToSetter);
```

#### Parameters

**feeToSetter** - The new address to set as the Fee To Setter

___

### Create Pair

Creates a new CRS/SRC pair contract if one does not already exist.

```C#
Address CreatePair(Address token);
```

#### Parameters

**token** - The SRC token used to create the CRS/SRC pairing

#### Returns

Created pair contract address

#### Logs

Model containing the address of the SRC token in the pair and the pair's new contract address

```C#
public struct PairCreatedEvent
{
    public Address Token;
    public Address Pair;
}
```

___

### Add Liquidity

Add liquidity to a pool for the provided token to receive liquidity pool tokens

```C#
AddLiquidityResponseModel AddLiquidity(Address token, ulong amountTokenDesired, ulong amountCrsMin, ulong amountTokenMin, Address to, ulong deadline);
```

#### Parameters

**token** - The token address to find a pair for

**amountTokenDesired** - The desired amount of SRC tokens to add

**amountCrsMin** - The minimum amount of CRS tokens to add

**amountTokenMin** - The minimum of SRC tokens to add

**to** - The address to receive the liquidity pool tokens at

**deadline** - Undecided if this should be implemented

#### Returns

Model containing the amount of SRC and CRS added to the pool and how many liquidity tokens were minted

```C#
public struct AddLiquidityResponseModel
{
    public ulong AmountCrs;
    public ulong AmountToken;
    public ulong Liquidity;
}
```

___

### Remove Liquidity

Removes liquidity from a pool for the provided token address, burns liquidity pool tokens

```C#
RemoveLiquidityResponseModel RemoveLiquidity(Address token, ulong liquidity, ulong amountCrsMin, ulong amountTokenMin, Address to, ulong deadline);
```

#### Parameters

**token** - The token address to find a pair for

**liquidity** - The amount of liquidity to burn

**amountCrsMin** - The minimum amount of CRS to receive for burning liquidity

**amountTokenMin** - The minimum amount of SRC to receive for burning liquidity

**to** - The address to send the CRS and SRC tokens to

**deadline** - Undecided if this should be implemented

#### Returns

Model containing the amount of CRS and SRC tokens removed from the liquidity pool

```C#
public struct RemoveLiquidityResponseModel
{
    public ulong AmountCrs;
    public ulong AmountToken;
}
```

___

### Swap Exact CRS Tokens For SRC Tokens

Equivalent to a CRS sell (e.g. Sell exactly 1 CRS for about 10 OPD)

```C#
void SwapExactCRSForTokens(ulong amountTokenOutMin, Address token, Address to, ulong deadline);
```

#### Parameters

**amountTokenOutMin** - 

**token** - 

**to** - 

**deadline** - 

___

### Swap SRC Tokens for Exact CRS Tokens

Equivalent to a SRC sell (e.g. Sell about 10 OPD for exactly 1 CRS)

```C#
void SwapTokensForExactCRS(ulong amountCrsOut, ulong amountTokenInMax, Address token, Address to, ulong deadline);
```

#### Parameters

**amountCrsOut** -

**amountTokenInMax** -

**token** -

**to** -

**deadline** -

___

### Swap Exact SRC Tokens for CRS Tokens

Equivalent to a SRC sell (e.g. Sell exactly 10 OPD for about 1 CRS)

```C#
void SwapExactTokensForCRS(ulong amountTokenIn, ulong amountCrsOutMin, Address token, Address to, ulong deadline);
```

#### Parameters

**amountTokenIn** -

**amountCrsOutMin** -

**token** -

**to** -

**deadline** -

___

### Swap CRS Tokens for Exact SRC Tokens

Equivalent to a CRS sell (e.g. Sell about 1 CRS for exactly 10 OPD)

```C#
void SwapCRSForExactTokens(ulong amountTokenOut, Address token, Address to, ulong deadline);
```

#### Parameters

**amountTokenOut** -

**token** -

**to** -

**deadline** -

___

### Get Liquidity Quote

```C#
ulong GetLiquidityQuote(ulong amountA, ulong reserveA, ulong reserveB);
```

#### Parameters

**amountA** -

**reserveA** -

**reserveB** -

#### Returns

___

### Get Amount Out

Calculates the token amount that will be transferred out based on the amount of tokens being sent in and the provided liquidity pool reserves.

```C#
ulong GetAmountOut(ulong amountIn, ulong reserveIn, ulong reserveOut);
```

#### Parameters

**amountIn** - The amount of the token to be sent into a pair

**reserveIn** - The reserves of the token that will be sent in (CRS reserves if amountIn is CRS, otherwise SRC reserves)

**reserveOut** - The reserves of the other token in the pool (SRC reserves if amountIn is CRS, otherwise CRS reserves)

#### Returns

Value of the amount of tokens that would be received
___

### Get Amount In

Calculates the token amount to be sent in based on the amount of tokens expected to be transferred out and the provided liquidity pool reserves.

```C#
ulong GetAmountIn(ulong amountOut, ulong reserveIn, ulong reserveOut);
```

#### Parameters

**amountOut** - The amount of tokens expected to be transferred out

**reserveIn** - The reserves of the token that will be sent in (SRC reserves if amountOut is CRS, otherwise CRS reserves)

**reserveOut** - The reserves of the other token in the pool (CRS reserves if amountOut is CRS, otherwise SRC reserves)

#### Returns

Value of the amount of tokens that are expected to be sent in

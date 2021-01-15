# Opdex V1 Pair Contract

An individual contract per pairing of CRS/SRC tokens. Each pair includes it's own liquidity pool and liquidity pool tokens. This contract should not be called directly outside of being called from the controller contract but it is not restricted. Calling these contracts directly while mutating contract state can result in loss of funds due to arbitrage between transactions. This is intentional.

## Public Methods

### Get Balance

Checks the balance of liquidity pool tokens of a provided address

```C#
ulong GetBalance(Address address);
```

#### Parameters

**address** - The address to check the balance of

#### Returns

Value of the address' liquidity pool token balance

___

### Allowance

Checks the allowance of liquidity pool tokens (This method is exactly the same as `GetAllowance` and is only included to meet the requirements of the IStandardToken interface)

```C#
ulong Allowance(Address owner, Address spender);
```

#### Parameters

**owner** - The owner of the liquidity pool tokens

**spender** - The spender to check the allowance of

#### Returns

Value of the spenders allowance from the owners address

___

### Get Allowance

Checks the allowance of liquidity pool tokens

```C#
ulong GetAllowance(Address owner, Address spender);
```

#### Parameters

**owner** - The owner of the liquidity pool tokens

**spender** - The spender to check the allowance of

#### Returns

Value of the spenders allowance from the owners address

___

### Transfer To

```C#
bool TransferTo(Address to, ulong amount);
```

#### Parameters

**to** - 

**amount** -

#### Returns

Success as boolean

___

### Transfer From

```C#
bool TransferFrom(Address from, Address to, ulong amount);
```

#### Parameters

**from** - 

**to** - 

**amount** -

#### Returns

Success as boolean

___

### Approve

```C#
// Supports IStandardToken Approve
bool Approve(Address spender, ulong currentAmount, ulong amount);
bool Approve(Address spender, ulong amount);
```

#### Parameters

**spender** -

**currentAmount** - value is unused in contract, only included because of IStandardToken interface inheritance

**amount** -

#### Returns

Success as boolean

___

### Mint

Mints new tokens based on differences in reserves and balances

```C#
ulong Mint(Address to);
```

#### Parameters

**to** - The address to transfer the minted tokens to

#### Returns

Number of liquidity tokens minted

___

### Burn

Burns pool tokens when removing liquidity

```C#
ulong[] Burn(Address to);
```

#### Parameters

**to** - The address to transfer CRS/SRC tokens to

#### Returns

___

### Swap

Swaps the incoming CRS or SRC token for an equal amount of the other token in the pair. Should be called from the controller contract directly, otherwise arbitrage may occur between transactions. Requires CRS or SRC token in amount to already have been sent to the pair's contract. Validates and transfers the requested amount out based on differences in the contracts balances and reserves

```C#
void Swap(ulong amountCrsOut, ulong amountTokenOut, Address to);
```

#### Parameters

**amountCrsOut** - The amount of CRS expected to be pulled out of the liquidity pool

**amountTokenOut** - The amount of SRC expected to be pulled out of the liquidity pools

**to** - The address to receive the tokens being sent out

___

### Skim

Forces this contracts balances to match reserves

```C#
void Skim(Address to);
```

#### Parameters

**to** - The address to send the difference to

___

### Sync

Forces the reserves amounts to match this contracts balances

```C#
void Sync();
```

___

### Get Reserves

Get the reserves of this pair's liquidity pool

```C#
ulong[] GetReserves();
```

#### Returns

Array with 2 values, first is CRS reserves, second is SRC reserves

___

## Logged Events

Includes all possible types of events that are logged from calling various pair contract methods. Event types can be queried individually through a Cirrus Full Node.

### Sync Event

```C#
public struct SyncEvent
{
    public ulong ReserveCrs;
    public ulong ReserveToken;
}
```

### Mint Event

```C#
public struct MintEvent
{
    [Index] public Address Sender;
    public ulong AmountCrs;
    public ulong AmountToken;
}
```

### Burn Event

```C#
public struct BurnEvent
{
    [Index] public Address Sender;
    [Index] public Address To;
    public ulong AmountCrs;
    public ulong AmountToken;
}
```

### Swap Event

```C#
public struct SwapEvent
{
    [Index] public Address Sender;
    [Index] public Address To;
    public ulong AmountCrsIn;
    public ulong AmountTokenIn;
    public ulong AmountCrsOut;
    public ulong AmountTokenOut;
}
```

### Approval Event

```C#
public struct ApprovalEvent
{
    [Index] public Address Owner;
    [Index] public Address Spender;
    public ulong Amount;
}
```

### Transfer Event

```C#
public struct TransferEvent
{
    [Index] public Address From;
    [Index] public Address To;
    public ulong Amount;
}
```

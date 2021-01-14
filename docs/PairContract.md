# Opdex V1 Pair Contract

An individual contract per pairing of CRS/SRC tokens. Each pair includes it's own liquidity pool and liquidity pool tokens. This contract should not be called directly outside of being called from the controller contract but it is not restricted. Calling these contracts directly while mutating contract state can result in loss of funds due to arbitrage between transactions. This is intentional.

___

## Public Methods

### Get Balance

```C#
/// <summary>
/// Checks the balance of liquidity pool tokens of a provided address
/// </summary>
/// <param name="address">The address to check the balance of</param>
/// <returns>Value of the address' liquidity pool token balance</returns>
ulong GetBalance(Address address);
```

### Allowance

```C#
/// <summary>
/// Checks the allowance of liquidity pool tokens (This method is exactly the same as `GetAllowance` 
/// and is only included to meet the requirements of the IStandardToken interface)
/// </summary>
/// <param name="owner">The owner of the liquidity pool tokens</param>
/// <param name="spender">The spender to check the allowance of</param>
/// <returns>Value of the spenders allowance from the owners address</returns>
ulong Allowance(Address owner, Address spender);
```

### Get Allowance

```C#
/// <summary>
/// Checks the allowance of liquidity pool tokens
/// </summary>
/// <param name="owner">The owner of the liquidity pool tokens</param>
/// <param name="spender">The spender to check the allowance of</param>
/// <returns>Value of the spenders allowance from the owners address</returns>
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
bool TransferTo(Address to, ulong amount);
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
/// <summary>
/// 
/// </summary>
/// <param name="spender"></param>
/// <param name="currentAmount"></param>
/// <param name="amount"></param>
/// <returns></returns>
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
/// Swaps the incoming CRS or SRC token for an equal amount of the other token in the pair. Should be called from the
/// controller contract directly, otherwise arbitrage may occur between transactions. Requires CRS or SRC token in amount
/// to already have been sent to the pair's contract. Validates and transfers the requested amount out based on
/// differences in the contracts balances and reserves
/// </summary>
/// <param name="amountCrsOut">The amount of CRS expected to be pulled out of the liquidity pool</param>
/// <param name="amountTokenOut">The amount of SRC expected to be pulled out of the liquidity pools</param>
/// <param name="to">The address to receive the tokens being sent out</param>
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
/// Get the reserves of this pair's liquidity pool
/// </summary>
/// <returns>Array with 2 values, first is CRS reserves, second is SRC reserves</returns>
ulong[] GetReserves();
```

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

# Opdex Standard Pool Contract

Standard liquidity pool contract that inherits for OpdexLiquidityPoolToken and IOpdexStandardPool. Used for standard pools that include swapping, adding and removing of liquidity. 

The transaction fee is 0.3% for swaps and 100% goes to liquidity providers in standard pools. 

## Overview

## Mint

Mints new liquidity pool tokens based on the differences between the contract's actual token balances and reserves. Adding liquidity through the 
controller contract optimistically transfers tokens to this contract before calling this method. 

The transfer of CRS and SRC to this contract and the call to this `Mint` method should be done in the same transaction, through the Controller
contract or an integrated 3rd party contract to avoid front-running, arbitrage, and/or loss of funds.

```C#
/// <summary>
/// When adding liquidity, mints new liquidity pool tokens based on differences in reserves and balances.
/// </summary>
/// <remarks>
/// Should be called from the Opdex controller contract normally with the exception of being called
/// from an integrated smart contract. Token transfers to the pool and this method should be
/// called in the same transaction to prevent arbitrage between separate transactions.
/// </remarks>
/// <param name="to">The address to assign the minted LP tokens to.</param>
/// <returns>The number if minted LP tokens</returns>
UInt256 Mint(Address to);
```

## Burn

Burns liquidity pool tokens based on the amount sent optimistically to this contract through the controller contract. The contract
checks its balance of liquidity pool tokens, burns them and returns the share of reserves to the user.

The transfer of liquidity pool tokens to this contract and the call of this `Burn` method should be done in the same transaction, through the Controller
contract or an integrated 3rd party contract to avoid front-running, arbitrage, and/or loss of funds.

```C#
/// <summary>
/// When removing liquidity, burns liquidity pool tokens returning an equal share of the pools reserves. 
/// </summary>
/// <remarks>
/// Should be called from the Opdex controller contract normally with the exception of being called
/// from an integrated smart contract. Token transfers to the pool and this method should be
/// called in the same transaction to prevent arbitrage between separate transactions.
/// </remarks>
/// <param name="to">The address to return the reserves tokens to</param>
/// <returns>Array of CRS and SRC amounts returned. (e.g. [ AmountCrs, AmountSrc ])</returns>
UInt256[] Burn(Address to);
```

## Swap

Swaps tokens in a pool based on the desired amount to pull out of the pool. Through the controller contract, the input token is transferred
prior to calling this method. The pool determines which and how many tokens were sent by checking the difference between the
pool's token balances and the last recorded reserves. If the amount sent does not satisfy the amount expected to be received, the
transaction fails and rolls back.

The transfer of CRS or SRC tokens to this contract and the call of this `Swap` method should be done in the same transaction, through the Controller
contract or an integrated 3rd party contract to avoid front-running, arbitrage, and/or loss of funds.

```C#
/// <summary>
/// Swap between token types in the pool, determined by differences in balances and reserves. 
/// </summary>
/// <remarks>
/// Should be called from the Opdex controller contract normally with the exception of being called
/// from an integrated smart contract. Token transfers to the pool and this method should be
/// called in the same transaction to prevent arbitrage between separate transactions.
/// </remarks>
/// <param name="amountCrsOut">The amount of CRS tokens to pull from the pool.</param>
/// <param name="amountSrcOut">The amount of SRC tokens to pull from the pool.</param>
/// <param name="to">The address to send the tokens to.</param>
/// <param name="data">
/// <see cref="CallbackData"/> bytes for a callback after tokens are pulled form the pool but before
/// validations enforcing the necessary input amount and fees.
/// </param>
void Swap(ulong amountCrsOut, UInt256 amountSrcOut, Address to, byte[] data);
```

## Skim

Forces the balances to equal the current token reserves. In the event of a "donation" or event alike, withdraw the difference between the 
balances overages and the reserve totals.

```C#
/// <summary>
/// Forces the pools balances to match the reserves, sending overages to the caller.
/// </summary>
/// <param name="to">The address to send any differences to</param>
void Skim(Address to);
```

## Sync

Forces the reserves to equal the current token balances. In the event of a "donation" or event alike, update the reserves to reflect the change.

```C#
/// <summary>
/// Updates the pools reserves to equal match the pools current token balances.
/// </summary>
void Sync();
```

---

Ported and adjusted based on https://github.com/Uniswap/uniswap-v2-core
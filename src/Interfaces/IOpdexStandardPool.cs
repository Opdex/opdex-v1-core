using Stratis.SmartContracts;
using Stratis.SmartContracts.Standards;

public interface IOpdexStandardPool : IStandardToken256
{
    /// <summary>
    /// The liquidity pool token's name.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// The liquidity pool token's ticker symbol.
    /// </summary>
    string Symbol { get; }
    
    /// <summary>
    /// The SRC token in the pool.
    /// </summary>
    Address Token { get; }
    
    /// <summary>
    /// Amount of CRS in reserves.
    /// </summary>
    ulong ReserveCrs { get; }
    
    /// <summary>
    /// Amount of SRC in reserves.
    /// </summary>
    UInt256 ReserveSrc { get; }
    
    /// <summary>
    /// The product of the reserves after the previous Mint or Burn transactions.
    /// </summary>
    UInt256 KLast { get; }
    
    /// <summary>
    /// Contract reentrant lock. 
    /// </summary>
    bool Locked { get; }
    
    /// <summary>
    /// List of reserve balances. (e.g. [ (ulong)AmountCrs, (UInt256)AmountSrc ])
    /// </summary>
    object[] Reserves { get; }
    
    /// <summary>
    /// Returns the CRS balance of the pool.
    /// </summary>
    ulong Balance { get; }
    
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
    
    /// <summary>
    /// Forces the pools balances to match the reserves, sending overages to the caller.
    /// </summary>
    /// <param name="to">The address to send any differences to</param>
    void Skim(Address to);
    
    /// <summary>
    /// Updates the pools reserves to equal match the pools current token balances.
    /// </summary>
    void Sync();

    /// <summary>
    /// Allows direct transfers of CRS tokens through the standard Transfer method to this contract.
    /// </summary>
    void Receive();
}
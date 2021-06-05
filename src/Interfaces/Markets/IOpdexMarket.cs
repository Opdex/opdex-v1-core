using Stratis.SmartContracts;

public interface IOpdexMarket
{
    /// <summary>
    /// The market transaction fee 0-10 equal to 0-1%.
    /// </summary>
    uint TransactionFee { get; }
    
    /// <summary>
    /// Retrieve a pool's contract address by the SRC token associated.
    /// </summary>
    /// <param name="token">The address of the SRC token.</param>
    /// <returns>The address of the requested pool.</returns>
    Address GetPool(Address token);
    
    /// <summary>
    /// Create a liquidity pool for the provided token if one does not already exist.
    /// </summary>
    /// <param name="token">The address of the SRC token.</param>
    /// <returns>The address of the created pool.</returns>
    Address CreatePool(Address token);
}
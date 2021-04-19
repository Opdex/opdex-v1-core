using Stratis.SmartContracts;

public interface IOpdexStandardPool : IOpdexPool
{
    /// <summary>
    /// The address of the market the pool is assigned to.
    /// </summary>
    Address Market { get; }
    
    /// <summary>
    /// Flag to authorize liquidity providing type transactions.
    /// </summary>
    bool AuthProviders { get; }
    
    /// <summary>
    /// Flag to authorize swap type transactions.
    /// </summary>
    bool AuthTraders { get; }

    /// <summary>
    /// Checks if an address is authorized for the requested permission. If authorizations are
    /// enabled will make a call to the Market contract to check permissions.
    /// </summary>s
    /// <param name="address">The address to check a permission authorization for.</param>
    /// <param name="permission">The permission to check authorization of. (1 - Create Pool, 2 - Trade, 3 - Provide, 4 - Set Permissions)</param>
    /// <returns>Flag describing if the address is authorized for the requested permission.</returns>
    bool IsAuthorizedFor(Address address, byte permission);

    /// <summary>
    /// Allows the current set market to assign the pool to a new market.
    /// </summary>
    /// <param name="address">The new market smart contract address.</param>
    void SetMarket(Address address);
}
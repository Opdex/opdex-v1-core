using Stratis.SmartContracts;

public interface IOpdexStandardMarket : IOpdexMarket
{
    /// <summary>
    /// Flag to authorize traders or not.
    /// </summary>
    bool AuthTraders { get; }
    
    /// <summary>
    /// Flag to authorize liquidity providers or not.
    /// </summary>
    bool AuthProviders { get; }

    /// <summary>
    /// Flag to authorize liquidity pool creators or not.
    /// </summary>
    bool AuthPoolCreators { get; }
    
    /// <summary>
    /// The address of the market owner.
    /// </summary>
    Address Owner { get; }
    
    /// <summary>
    /// Checks the if the provided address is authorized for the given permission.
    /// </summary>
    /// <param name="address">The address to check permissions for.</param>
    /// <param name="permission">
    /// The permission to check authorization of. See <see cref="Permissions"/> for list of available options.
    /// </param>
    /// <returns>Flag describing if the address is authorized or not.</returns>
    bool IsAuthorizedFor(Address address, byte permission);

    /// <summary>
    /// Allows permitted addresses to sets authorization for a provided address and permission.
    /// </summary>
    /// <param name="address">The address to set the permission for.</param>
    /// <param name="permission">The permission being set. See <see cref="Permissions"/> for list of available options.</param>
    /// <param name="authorize">Flag describing if the address should be authorized or not.</param>
    void Authorize(Address address, byte permission, bool authorize);

    /// <summary>
    /// Allows the existing market owner to assign a new market owner.
    /// </summary>
    /// <param name="address">The new market owner to promote.</param>
    void SetOwner(Address address);

    /// <summary>
    /// Allows the owner to change the persisted market contract address for a pool.
    /// Enables market contracts to be updated to allow extra functionality or improving flows.
    /// </summary>
    /// <param name="token">The SRC token to lookup the pool being updated.</param>
    /// <param name="newMarket">The new market's smart contract address.</param>
    void SetPoolMarket(Address token, Address newMarket);
}
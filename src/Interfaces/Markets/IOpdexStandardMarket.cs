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
    /// 
    /// </summary>
    bool MarketFeeEnabled { get; }
    
    /// <summary>
    /// The address of the market owner.
    /// </summary>
    Address Owner { get; }
    
    /// <summary>
    /// Checks if the provided address is authorized for the given permission.
    /// </summary>
    /// <param name="address">The address to check permissions for.</param>
    /// <param name="permission">
    /// The permission to check authorization of. See <see cref="Permissions"/> for list of available options.
    /// </param>
    /// <returns>Flag describing if the address is authorized or not.</returns>
    bool IsAuthorized(Address address, byte permission);

    /// <summary>
    /// Checks if the provided primary and secondary addresses are authorized for the given permission.
    /// </summary>
    /// <param name="primary">The primary address to check permissions for.</param>
    /// <param name="secondary">The secondary address to check permissions for.</param>
    /// <param name="permission">The permission to check authorizations for.</param>
    /// <returns></returns>
    bool IsAuthorized(Address primary, Address secondary, byte permission);

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
    /// 
    /// </summary>
    /// <param name="token"></param>
    /// <param name="amount"></param>
    void CollectMarketFees(Address token, UInt256 amount);
}
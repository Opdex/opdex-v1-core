using Stratis.SmartContracts;

public interface IOpdexStandardMarket : IOpdexMarket
{
    /// <summary>
    /// Flag that enables authorizing traders before they have access to the market.
    /// </summary>
    bool AuthTraders { get; }
    
    /// <summary>
    /// Flag that enables authorizing providers before they have access to the market.
    /// </summary>
    bool AuthProviders { get; }

    /// <summary>
    /// Flag that enables authorization pool creators before they have access to the market.
    /// </summary>
    bool AuthPoolCreators { get; }
    
    /// <summary>
    /// The address of the market owner.
    /// </summary>
    Address Owner { get; }
    
    /// <summary>
    /// Checks if the provider address has the requested permissions.
    /// </summary>
    /// <param name="address"></param>
    /// <param name="permission">
    /// The permission being requested. See <see cref="Permissions"/> for list of available options.
    /// </param>
    /// <returns>Flag describing if the address has permission or not.</returns>
    bool IsAuthorizedFor(Address address, byte permission);

    /// <summary>
    /// Allows permitted addresses to authorize permissions of other addresses.
    /// </summary>
    /// <param name="address">The address to set permissions for.</param>
    /// <param name="permission">The permission being updated. See <see cref="Permissions"/> for list of available options.</param>
    /// <param name="isAuthorized">Flag describing if the user should be authorized or not.</param>
    void Authorize(Address address, byte permission, bool isAuthorized);

    /// <summary>
    /// Allows the existing market owner to assign a new market owner.
    /// </summary>
    /// <param name="address">The new market owner to promote.</param>
    void SetOwner(Address address);
}
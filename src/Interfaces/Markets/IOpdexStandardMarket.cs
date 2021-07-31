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
    /// Flag indicating if the market owner collects 1/6 of all transaction fees.
    /// </summary>
    bool MarketFeeEnabled { get; }

    /// <summary>
    /// The wallet address of the active owner of the market.
    /// </summary>
    /// <remarks>
    /// The market owner's privileges include the ability to adjust any permission, qualification for any permission,
    /// and is the collector of any market fees accrued when enabled.
    /// </remarks>
    Address Owner { get; }

    /// <summary>
    /// A pending wallet address that has been suggested to take ownership of the contract. This value
    /// acts as a whitelist for access to <see cref="ClaimPendingOwnership"/>.
    /// </summary>
    Address PendingOwner { get; }

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
    /// <returns>Flag describing if the addresses are authorized or not.</returns>
    bool IsAuthorized(Address primary, Address secondary, byte permission);

    /// <summary>
    /// Allows permitted addresses to set an authorization for a provided address and permission.
    /// </summary>
    /// <param name="address">The address to set the permission for.</param>
    /// <param name="permission">The permission being set. See <see cref="Permissions"/> for list of available options.</param>
    /// <param name="authorize">Flag describing if the address should be authorized or not.</param>
    void Authorize(Address address, byte permission, bool authorize);

    /// <summary>
    /// Public method allowing the current contract owner to whitelist a new pending owner. The newly pending owner
    /// will then call <see cref="ClaimPendingOwnership"/> to accept.
    /// </summary>
    /// <param name="pendingOwner">The address to set as the new pending owner.</param>
    void SetPendingOwnership(Address pendingOwner);

    /// <summary>
    /// Public method to allow the pending new owner to accept ownership replacing the current contract owner.
    /// </summary>
    void ClaimPendingOwnership();

    /// <summary>
    /// Looks up a pool by the SRC token and transfers any Market contract owned fees (LP tokens) in the pool to the current <see cref="Owner"/> of the market.
    /// </summary>
    /// <remarks>
    /// See <see cref="IOpdexLiquidityPool.GetBalance"/> for retrieving the current amount of fees available for collection held by the market contract.
    /// </remarks>
    /// <param name="token">The SRC token address to lookup the liquidity pool by.</param>
    /// <param name="amount">The amount of fees (LP tokens) to collect from the pool and transfer to the market owner.</param>
    void CollectMarketFees(Address token, UInt256 amount);
}
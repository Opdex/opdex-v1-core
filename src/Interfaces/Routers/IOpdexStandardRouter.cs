public interface IOpdexStandardRouter : IOpdexRouter
{
    /// <summary>
    /// Flag indicating if liquidity providers should be authorized
    /// </summary>
    bool AuthProviders { get; }
        
    /// <summary>
    /// Flag indicating if traders should be authorized
    /// </summary>
    bool AuthTraders { get; }
}
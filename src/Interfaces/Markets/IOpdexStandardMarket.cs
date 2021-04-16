using Stratis.SmartContracts;

public interface IOpdexStandardMarket : IOpdexMarket
{
    /// <summary>
    /// 
    /// </summary>
    bool AuthorizeTraders { get; }
    
    /// <summary>
    /// 
    /// </summary>
    bool AuthorizeProviders { get; }

    /// <summary>
    /// 
    /// </summary>
    bool AuthorizePoolCreators { get; }
    
    /// <summary>
    /// 
    /// </summary>
    Address Owner { get; }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="address"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    bool IsAuthorizedFor(Address address, byte action);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="address"></param>
    /// <param name="authorization"></param>
    /// <param name="isAuthorized"></param>
    void Authorize(Address address, byte authorization, bool isAuthorized);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="address"></param>
    void SetOwner(Address address);
}
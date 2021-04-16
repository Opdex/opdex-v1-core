using Stratis.SmartContracts;

public interface IOpdexMarketDeployer
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="authPoolCreators"></param>
    /// <param name="authProviders"></param>
    /// <param name="authTraders"></param>
    /// <param name="fee"></param>
    /// <returns></returns>
    Address CreateStandardMarket(bool authPoolCreators, bool authProviders, bool authTraders, uint fee);
}
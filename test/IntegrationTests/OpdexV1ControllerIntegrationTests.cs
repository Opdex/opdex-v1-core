using Xunit;

namespace OpdexV1Contracts.Tests.IntegrationTests
{
    public class OpdexV1ControllerIntegrationTests : IClassFixture<BaseIntegrationTest>
    {
        private readonly BaseIntegrationTest _fixture;
        
        public OpdexV1ControllerIntegrationTests(BaseIntegrationTest fixture)
        {
            _fixture = fixture;
        }
    }
}
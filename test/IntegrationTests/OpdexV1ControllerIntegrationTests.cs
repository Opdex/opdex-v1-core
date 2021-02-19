using FluentAssertions;
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

        // [Fact]
        // public void CreatesControllerContract()
        // {
        //     using (var chain = _fixture.Chain)
        //     {
        //         chain.Should().NotBeNull();
        //     }
        // }
    }
}
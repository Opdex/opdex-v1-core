using FluentAssertions;
using Xunit;

namespace OpdexCoreContracts.Tests.IntegrationTests
{
    public class OpdexControllerIntegrationTests : IClassFixture<BaseIntegrationTest>
    {
        private readonly BaseIntegrationTest _fixture;
        
        public OpdexControllerIntegrationTests(BaseIntegrationTest fixture)
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
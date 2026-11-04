namespace Subscript.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<SubscriptApiFactory>
{
    public const string Name = "integration";
}

using Content.Server.Spawners.Components;
using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._AS;

[TestFixture]
public sealed class ConflictingComponentsTest
{
    private static readonly (Type, Type)[] ConflictingComponents =
    [
        (typeof(RandomSpawnerComponent), typeof(EntityTableSpawnerComponent)),
    ];

    [Test]
    public async Task CheckServerConflictingComponents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var protoManager = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var entity in protoManager.EnumeratePrototypes<EntityPrototype>())
                {
                    foreach (var component in ConflictingComponents)
                    {
                        Assert.That(entity.HasComponent(component.Item1) && entity.HasComponent(component.Item2), Is.False, $"Entity {entity} contains conflicting components {component.Item1} and {component.Item2}.");
                    }
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}

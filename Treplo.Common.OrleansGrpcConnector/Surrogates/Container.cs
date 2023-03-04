namespace Treplo.Common.OrleansGrpcConnector.Surrogates;

[GenerateSerializer]
public readonly struct Container
{
    public Container(string name)
    {
        Name = name;
    }

    [Id(0)] public readonly string Name;
}

[RegisterConverter]
public sealed class ContainerConverter : IConverter<Common.Container, Container>
{
    public Common.Container ConvertFromSurrogate(in Container surrogate) => new()
    {
        Name = surrogate.Name,
    };

    public Container ConvertToSurrogate(in Common.Container value) => new(value.Name);
}
## Configuring types
You can configure types by calling `ConfigureTypes` method of `GqlContext` instance.
```csharp
...
context.ConfigureTypes(CongigureTypes);
...
private void ConfigureTypes(IGqlTypesConfigurator configurator)
{
   ...
}
```
or
```csharp
context.ConfigureTypes(configurator => ...);
```
and then call `Configure<T>` method of `IGqlTypesConfigurator` to configure type, e.g.
```csharp
configurator.Configure<Product>(cfg => ...);
```
### Mapping CLR type on GraphQL type
Mapping CLR type on GraphQL type may be usefull in next situations:
* CLR type name differs from GraphQL type name;
* you are using inheritance and reading object of abstract type (in this case type name is defined in GraphQL response and must be mapped on CLR type).

To map CLR type on GraphQL type you need to call `HasTypeName` method of `IGqlTypeConfigurator<T>` and specify GraphQL type for configured type.
```csharp
configurator.Configure<EReader>(cfg => cfg.HasTypeName("EReader"));
```
### Mapping property on GraphQL field
Mapping property on GraphQL field may be usefull when CLR type property name differs from GraphQL field name.

To map CLR type property on GraphQL field you need to call `HasField` method of `IGqlTypeConfigurator<T>` and specify type member and mapped GraphQL field name.
```csharp
configurator.Configure<EReader>(cfg => cfg.HasField(_ => _.EInkGeneration, "inkType"));
```
### Declaring custom factory for type
Declaring custom factory for type may be usefull for types that has no default constructor or must use specific constructor during deserialization.

To declare custom factory for type you need to call `ConstructWith` method of `IGqlTypeConfigurator<T>` and specify factory delegate.
```csharp
configurator.Configure<EReader>(cfg => cfg.ConstructWith(() => new EReader(manufacturer)));
```
### Complete example
```csharp
context
    .ConfigureTypes(configurator =>
        configurator
            .Configure<EReader>(cfg => cfg
                .HasField(_ => _.EInkGeneration, "inkType")
                .HasTypeName("EReader")
                .ConstructWith(() => new EReader(manufacturer))))
```

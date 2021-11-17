## Fetching data
### Simple query
Common point for fetching data is `GqlContext`.
After creating instance of `GqlContext` and [configuring](https://github.com/ugparu/Ugpa.GraphQL.Linq/blob/doc/doc/ConfiguringTypes.md#configuring-types) all required types, you can fetch data by calling `Get<T>()` method.
`GqlContext` automatically will find field of required type, based on configuration, and build query to fetch data from it.
For example
```
context.Get<Product>().ToArray()
```
will find parameterless field of type `Product` and regardless of it's name will fetch data from this field.
### Parametrised query
Assumes we have endpoint with parametrised field
```gql
type Query {
  customerProducts(customer: String!): [Product!]!
}
```
To fetch data from this endpoint you need to use Where constraint like above
```csharp
context.Get<Product>().Where(new { customer = "Customer Name" }).ToArray()
```
GqlContext automatically will find field of type Product with customer parameter and generate query to fetch data from it.
Value "Customer Name" will be sent as query variable, and query will look like
```gql
query($linq_param_0: String!) {
  customerProducts(customer: $linq_param_0) {
    manufacturer
  }
}
```

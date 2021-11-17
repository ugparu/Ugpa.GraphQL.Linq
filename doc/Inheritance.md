## Inheritance
### Interfaces
Assumes we have next GraphQL types:
```gql
interface Product {
  manufacturer: String!
}
type Smartphone implements Product {
  manufacturer: String!
  operationSystem: String!
}
type EReader implements Product {
  manufacturer: String!
  inkType: String!
}
```
C# model for it will be like this:
```csharp
abstract class Product
{
    public string Manufacturer { get; set; }
}
class Smartphone: Product
{
    public string OperationSystem { get; set; }
}
class EReader: Product
{
    public string InkType { get; set; }
}
```
Now when you will fetch data from GraphQL endpoint, for each member of type `Product` field `__typename` will be included automatically. For example this code
```csharp
context.Get<Product>().ToArray();
```
will generate query
```gql
query {
  products {
    __typename
    manufacturer
    ... on Smartphone {
      operationSystem
    }
    ... on EReader {
      inkType
    }
  }
}
```
### Unions
Assumes we have next GraphQL types:
```gql
type Smartphone {
  manufacturer: String!
  operationSystem: String!
}
type EReader {
  manufacturer: String!
  inkType: String!
}
union Product = Smartphone | EReader
```
C# model for it will be same as above for interface types. It's important to keep in mind that C# types `Smartphone` and `EREader` must have common base type (like `Product`).
Now when you will fetch data from endpoint, next query will be generated
```gql
query {
  products {
    __typename
    ... on Smartphone {      
      manufacturer
      operationSystem
    }
    ... on EReader {
      manufacturer
      inkType
    }
  }
}
```
### Important notes
* For correct work of inheritance model all type must be configured as discribed in [this](https://github.com/ugparu/Ugpa.GraphQL.Linq/blob/doc/doc/ConfiguringTypes.md#mapping-clr-type-on-graphql-type) section.

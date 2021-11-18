## Fetching data
### Simple query
Common point for fetching data is `GqlContext`.
After creating instance of `GqlContext` and [configuring](https://github.com/ugparu/Ugpa.GraphQL.Linq/blob/doc/doc/ConfiguringTypes.md#configuring-types) all required types, you can fetch data by calling `Get<T>()` method.
`GqlContext` automatically will find field of required type, based on configuration, and build query to fetch data from it.
For example
```csharp
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
To fetch data from this endpoint you need to use `Where` constraint like above
```csharp
context.Get<Product>().Where(new { customer = "Customer Name" }).ToArray()
```
`GqlContext` automatically will find field of type `Product` with `customer` parameter and generate query to fetch data from it.
Value "Customer Name" will be sent as query variable, and query will look like
```gql
query($linq_param_0: String!) {
  customerProducts(customer: $linq_param_0) {
    manufacturer
  }
}
```
## Fetching nested members
By default, when fetching data, only scalar fields are included in query. For example, assumes manufacturer is complex type
```gql
type Manufacturer {
  name: String!
  location: Address!
  products: [Product!]!
}
type Address {
  country: String!
  city: String!
}
type Product {
  name: String!
  manufacturer: Manufacturer!
}
```
When you will fetch `Product` by this way
```csharp
context.Get<Product>().ToArray()
```
only it's `name` field will be included and generated query will be like this
```gql
query {
  products {
    name
  }
}
```
### Including data
To fetch produtcs including it's manufacturer, you need to use `Include` extension method, like described above
```csharp
context.Get<Product>()
    .Include(_ => _.Manufacturer)
    .ToArray()
```
In this case, all scalar fields of `Product` will be included in query, `manufacturer` field and all scalar fields of `Manufacturer`. Generated query text will be next
```gql
query {
  products {
    name
    manufacturer {
      name
    }
  }
}
```
To include manufacturer and it's address, you need to call `Include` twice
```csharp
context.Get<Product>()
    .Include(_ => _.Manufacturer)
    .Include(_ => _.Manufacturer.Address)
    .ToArray()
```
Generated query will be next
```gql
query {
  products {
    name
    manufacturer {
      name
      address {
        country
        city
      }
    }
  }
}
```
To include only manufactirer address, you need to call `Include` only for `Manufacturer.Address`
```csharp
context.Get<Product>().Include(_ => _.Manufacturer.Address).ToArray()
```
In this case `Manufacturer` scalar field will be skipped, and generated query will be next
```gql
query {
  products {
    name
    manufacturer {
      address {
        country
        city
      }
    }
  }
}
```
### Selecting data
If you need to fetch only nested fields, e.g., you need only manufacturer data from products, you can use `Select` extension method. For example
```csharp
context.Get<Product>().Select(_ => _.Manufacturer).ToArray()
```
In this case all fields of product will be inored except `manufacturer` field, and generated query wiil be
```gql
query {
  products {
    manufacturer {
      address {
        country
        city
      }
    }
  }
}
```
Result of this request will be array of `Manufacturer`.

Extension method `SelectMany` is also supported to fetch collections
```csharp
context.Get<Product>().Select(_ => _.Manufacturer).SelectMany(_ => _.AllProducts).ToArray()
```
or
```csharp
context.Get<Product>().SelectMany(_ => _.Manufacturer.AllProducts).ToArray()
```

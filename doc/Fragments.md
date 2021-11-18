## Fragments
To optimize generated query, you can use fragments. To define fragment you need to call extension method `UsingFragment`. Assumes, we have next GraphQL schema
```gql
type Smartphone {
  manufacturer: Manufacturer!
  operationSystem: String!
  eSim: Boolean!
}
type Tablet {
  manufacturer: Manufacturer!
  operationSystem: String!
  hasKeyboard: Boolean!
}
type EReader {
  manufacturer: Manufacturer!
  inkType: String!
}
union Product = Smartphone | Tablet | EREader
type Manufacturer {
  name: String!
  country: String!
  city: String!
}
```
When you will fetch product data including manufacturer
```csharp
context.Get<Product>().Include(_ => _.Manufacturer).ToArray()
```
generated query will be next
```gql
query {
  products {
    __typename
    ... on Smartphone {    
      operationSystem
      eSim
      manufacturer {
        name
        country
        city
      }
    }
    ... on Tablet {      
      operationSystem
      hasKeyboard
      manufacturer {
        name
        country
        city
      }
    }
    ... on EReader {
      inkType
      manufacturer {
        name
        country
        city
      }
    }
  }
}
```
As you can see, `manufacturer` field apeares for each product and contains all it's fields.
To optimaze this query, you can define ftagment for `Manufacturer` like shown below
```csharp
context.Get<Product>()
    .Include(_ => _.Manufacturer)
    .UsingFragment((IQueryable<Manufacturer> manufacturerFragment) => manufacturerFragment)
    .ToArray()
```
Parameter name `manufacturerFragment` will be used as fragment name.
In this case for for `Manufacturer` type will be defined fragment and it will be used for each field of `Manufacturer` type, and result query for example above will be
```gql
query {
  products {
    __typename
    ... on Smartphone {    
      operationSystem
      eSim
      manufacturer {
        ... manufacturerFragment
      }
    }
    ... on Tablet {      
      operationSystem
      hasKeyboard
      manufacturer {
        ... manufacturerFragment
      }
    }
    ... on EReader {
      inkType
      manufacturer {
        ... manufacturerFragment
      }
    }
  }
}
fragment manufacturerFragment on Manufacturer {
  name
  country
  city
}
```
You can define multiple fragments at once. For example, you can create fragment for `Manufacturer` and `Product` like this
```csharp
context.Get<Product>()
    .Include(_ => _.Manufacturer)
    .UsingFragment((IQueryable<Manufacturer> manufacturerFragment) => manufacturerFragment)
    .UsingFragment((IQueryable<Product> productFragment) => productFragment)
    .ToArray()
```
Generated query in this case will be
```gql
query {
  products {
    ... productFragment
    ... on Smartphone {    
      manufacturer {
        ... manufacturerFragment
      }
    }
    ... on Tablet {
      manufacturer {
        ... manufacturerFragment
      }
    }
    ... on EReader {
      manufacturer {
        ... manufacturerFragment
      }
    }
  }
}
fragment manufacturerFragment on Manufacturer {
  name
  country
  city
}
fragment productFragment on Product {
  __typename
  ... on Smartphone {    
    operationSystem
    eSim
  }
  ... on Tablet {      
    operationSystem
    hasKeyboard
  }
  ... on EReader {
    inkType
  }
}
```
Extension method `Include` can be used inside fragments. For example
```csharp
context.Get<Product>()
    .UsingFragment((IQueryable<Manufacturer> manufacturerFragment) => manufacturerFragment)
    .UsingFragment((IQueryable<Product> productFragment) => productFragment.Include(_ => _.Manufacturer))
    .ToArray()
```
In this case generated query will be next
```gql
query {
  products {
    ... productFragment
  }
}
fragment manufacturerFragment on Manufacturer {
  name
  country
  city
}
fragment productFragment on Product {
  __typename
  ... on Smartphone {    
    operationSystem
    eSim
    manufacturer {
      ... manufacturerFragment
    }
  }
  ... on Tablet {      
    operationSystem
    hasKeyboard
    manufacturer {
      ... manufacturerFragment
    }
  }
  ... on EReader {
    inkType
    manufacturer {
      ... manufacturerFragment
    }
  }
}
```
## External fragment definitions
In some scenarios you may need to declare fragment externaly before fetching data from endpoint, e.g., you are using some extensibility mechanism to prebuild fragment.
In this case you can use another overload of `UsingFragment` method, like shown above
```csharp
(Func<IQueryable<Product>, IQueryable<Product>>) productFragment = BuildProductFragment();

context
    .Get<Product>()
    .UsingFragment("productFragment", productFragment)
    .ToArray();
```
Fragment name here is defined by string paramenter "productFragment".

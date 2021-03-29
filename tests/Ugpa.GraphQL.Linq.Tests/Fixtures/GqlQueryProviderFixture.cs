namespace Ugpa.GraphQL.Linq.Tests.Fixtures
{
    public sealed class GqlQueryProviderFixture
    {
        public GqlQueryProviderFixture()
        {
            var schema = global::GraphQL.Types.Schema.For(@"
type ProductAboutType {
    stamp: String
}
type ProductInfoType {
    title: String!
    version: Int
    about: ProductAboutType
}
type DrawSchemaType {
    id: ID!
    name: String!
}
type ProductType {
    id: Int!
    name: String!
    comment: String
    productInfo: ProductInfoType!
    schemas: [DrawSchemaType]
}
type Query {
    products: [ProductType]
    schemas(productId: Int!): [DrawSchemaType]
}
");

            Provider = new GqlQueryProvider(null, schema);
        }

        internal GqlQueryProvider Provider { get; }
    }
}

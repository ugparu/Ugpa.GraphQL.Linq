using System;

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
                interface TypeTemplateInterface {
                    id: ID
                    name: String!
                }
                type TextTemplateType implements TypeTemplateInterface {
                    id: ID
                    name: String!
                    fontFamily: String!
                    fontSize: Float!
                }
                type RailchainTemplateType implements TypeTemplateInterface {
                    id: ID
                    name: String!
                    mainLineWidth: Int!
                    sideLineWidth: Int!
                }
                type Query {
                    products: [ProductType]
                    schemas(productId: Int!): [DrawSchemaType]
                    templates: [TypeTemplateInterface]
                }
                ",
                _ =>
                {
                    _.Types.For("TypeTemplateInterface").ResolveType = obj => throw new NotSupportedException();
                });

            Provider = new GqlQueryProvider(null, schema);
        }

        internal GqlQueryProvider Provider { get; }
    }
}

using System;
using GraphQL.Types;

namespace Ugpa.GraphQL.Linq.Tests.Fixtures
{
    public sealed class GqlSchemaFixture
    {
        public GqlSchemaFixture()
        {
            Schema = global::GraphQL.Types.Schema.For(@"
                type ProductAbout {
                    stamp: String
                }
                type ProductInfo {
                    title: String!
                    version: Int
                    about: ProductAbout
                }
                type DrawSchema {
                    id: ID!
                    name: String!
                }
                type Product {
                    id: Int!
                    name: String!
                    comment: String
                    productInfo: ProductInfo!
                    schemas: [DrawSchema]
                }
                interface TypeTemplate {
                    id: ID
                    name: String!
                }
                type TextTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    fontFamily: String!
                    fontSize: Float!
                }
                type RailchainTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    mainLineWidth: Int!
                    sideLineWidth: Int!
                }
                type Query {
                    products: [Product]
                    product(productId: Int!): Product!
                    schemas(productId: Int!): [DrawSchema]
                    templates: [TypeTemplate]
                }
                ",
                _ =>
                {
                    _.Types.For("TypeTemplate").ResolveType = obj => throw new NotSupportedException();
                });

            Schema.Initialize();
        }

        public ISchema Schema { get; }

        internal sealed class Product
        {
            public ProductInfo ProductInfo { get; set; }

            public DrawSchema[] Schemas { get; set; }
        }

        internal sealed class ProductInfo
        {
            public ProductAbout About { get; set; }
        }

        internal sealed class ProductAbout
        {
        }

        internal sealed class DrawSchema
        {
        }

        internal class TypeTemplate
        {
        }
    }
}

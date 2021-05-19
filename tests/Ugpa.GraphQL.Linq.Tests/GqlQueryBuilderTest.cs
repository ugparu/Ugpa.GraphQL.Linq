using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GraphQL.Types;
using GraphQL.Utilities;
using Moq;
using Ugpa.GraphQL.Linq.Utils;
using Xunit;

namespace Ugpa.GraphQL.Linq.Tests
{
    public class GqlQueryBuilderTest
    {
        private readonly IGraphTypeNameMapper mapper;

        public GqlQueryBuilderTest()
        {
            var mapperMock = new Mock<IGraphTypeNameMapper>();
            mapperMock.Setup(_ => _.GetTypeName(It.IsAny<Type>())).Returns((Type t) => t.Name);
            mapper = mapperMock.Object;
        }

        [Fact]
        public void SimpleQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type ProductInfo {
                    version: String!
                }
                type Product {
                    id: ID
                    name: String!
                    comment: String!
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0].AsQueryable();

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { id name comment } }", queryText);
        }

        [Fact]
        public void SingleSelectQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type About {
                    developer: String
                }
                type ProductInfo {
                    title: String!
                    version: String!
                    about: About!
                }
                type Product {
                    id: Int!
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Select(_ => _.ProductInfo);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);

            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { productInfo { title version } } }", queryText);
        }

        [Fact]
        public void NestedPropertySelectQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type About {
                    stamp: String!
                }
                type ProductInfo {
                    title: String!
                    about: About!
                }
                type Product {
                    id: Int!
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Select(_ => _.ProductInfo.About);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { productInfo { about { stamp } } } }", queryText);
        }

        [Fact]
        public void ChainedSelectQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type About {
                    stamp: String!
                }
                type ProductInfo {
                    title: String!
                    about: About!
                }
                type Product {
                    id: Int!
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Select(_ => _.ProductInfo)
                .Select(_ => _.About);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { productInfo { about { stamp } } } }", queryText);
        }

        [Fact]
        public void SelectManyQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Schema {
                    id: ID
                    name: String!
                }
                type Product {
                    id: Int!
                    schemas: [Schema]
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .SelectMany(_ => _.Schemas);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { schemas { id name } } }", queryText);
        }

        [Fact]
        public void SingleIncludeQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type ProductInfo {
                    title: String!
                    version: String!
                }
                type Product {
                    id: ID
                    name: String!
                    comment: String
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Include(_ => _.ProductInfo);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { id name comment productInfo { title version } } }", queryText);
        }

        [Fact]
        public void NestedIncludeQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type About {
                    stamp: String!
                }
                type ProductInfo {
                    comment: String!
                    about: About!
                }
                type Product {
                    id: ID
                    name: String!
                    comment: String
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Include(_ => _.ProductInfo.About);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { id name comment productInfo { about { stamp } } } }", queryText);
        }

        [Fact]
        public void MultipleIncludesQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type About {
                    stamp: String!
                }
                type ProductInfo {
                    title: String!
                    version: String!
                    about: About!
                }
                type Product {
                    id: ID
                    name: String!
                    comment: String
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Include(_ => _.ProductInfo)
                .Include(_ => _.ProductInfo.About);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { id name comment productInfo { title version about { stamp } } } }", queryText);
        }

        [Fact]
        public void SimpleParametrizedQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type DrawSchema {
                    id: ID
                    name: String!
                }
                type Query {
                    schemas(productId: Int!): [DrawSchema]
                }");

            var query = new DrawSchema[0]
                .AsQueryable()
                .Where(new { productId = 111 });

            var variablesResolver = new VariablesResolver();
            var queryText = queryBuilder.BuildQuery(query.Expression, variablesResolver, out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("schemas", entryPoint);
            Assert.Equal("query($linq_param_0: Int!) { schemas(productId: $linq_param_0) { id name } }", queryText);

            var variable = Assert.Single(variablesResolver.GetAllVariables());
            Assert.Equal("linq_param_0", variable.Name);
            Assert.Equal(111, variable.Value);
        }

        [Fact]
        public void ParametrizedSelectQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type ProductInfo {
                    title: String!
                    version: String!
                }
                type Product {
                    id: ID
                    productInfo: ProductInfo!
                }
                type Query {
                    products: [Product]
                    product(productId: Int!): Product
                }");

            var query = new Product[0]
                .AsQueryable()
                .Where(new { productId = 111 })
                .Select(_ => _.ProductInfo);

            var variablesResolver = new VariablesResolver();
            var queryText = queryBuilder.BuildQuery(query.Expression, variablesResolver, out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("product", entryPoint);
            Assert.Equal("query($linq_param_0: Int!) { product(productId: $linq_param_0) { productInfo { title version } } }", queryText);

            var variable = Assert.Single(variablesResolver.GetAllVariables());
            Assert.Equal("linq_param_0", variable.Name);
            Assert.Equal(111, variable.Value);
        }

        [Fact]
        public void InterfaceImplementationQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                interface TypeTemplate {
                    id: ID
                    name: String!
                }
                type TextTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    fontFamily: String!
                    fontSize: Int!            
                }
                type RailchainTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    mainLineWidth: Int!
                    sideLineWidth: Int!
                }
                type TextboxFrame {
                    borderWidth: Int!
                }
                type TextboxTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    frame: TextboxFrame!
                }
                type Query {
                    templates: [TypeTemplate]
                }",
                cfg =>
                {
                    cfg.Types.For("TypeTemplate").ResolveType = _ => throw new NotImplementedException();
                });

            var query = new TypeTemplate[0].AsQueryable();

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("templates", entryPoint);
            Assert.Equal(
                "query { templates { __typename id name " +
                "... on TextTemplate { fontFamily fontSize } " +
                "... on RailchainTemplate { mainLineWidth sideLineWidth } " +
                "} }",
                queryText);
        }

        [Fact]
        public void IncludeSubtypeFieldQueryTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                interface TypeTemplate {
                    id: ID
                    name: String!
                }
                type TextTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    fontFamily: String!
                    fontSize: Int!
                }
                type RailchainTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    mainLineWidth: Int!
                    sideLineWidth: Int!
                }
                type TextboxFrame {
                    borderWidth: Int!
                }
                type TextboxTemplate implements TypeTemplate {
                    id: ID
                    name: String!
                    frame: TextboxFrame!
                }
                type Query {
                    templates: [TypeTemplate]
                }",
                cfg =>
                {
                    cfg.Types.For("TypeTemplate").ResolveType = _ => throw new NotImplementedException();
                });

            var query = new TypeTemplate[0]
                .AsQueryable()
                .Include(_ => ((TextboxTemplate)_).Frame);

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("templates", entryPoint);

            Assert.Equal(
                "query { templates { __typename id name " +
                "... on TextTemplate { fontFamily fontSize } " +
                "... on RailchainTemplate { mainLineWidth sideLineWidth } " +
                "... on TextboxTemplate { frame { borderWidth } } " +
                "} }",
                queryText);
        }

        [Fact]
        public void NestedCollectionIncludeTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type DrawSchemaInfo {
                    author: String!
                }
                type DrawSchema {
                    id: Int!
                    info: DrawSchemaInfo!
                }
                type Product {
                    id: Int!
                    schemas: [DrawSchema]
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0]
                .AsQueryable()
                .Include(p => p.Schemas.Include(s => s.Info));

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query { products { id schemas { id info { author } } } }", queryText);
        }

        [Fact]
        public void NestedCollectionCastIncludeTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type DrawSchemaInfo {
                    author: String!
                }
                interface DrawSchema {
                    id: Int!
                    info: DrawSchemaInfo!
                }
                type SimpleDrawSchema implements DrawSchema {
                    id: Int!
                    info: DrawSchemaInfo!
                }
                type ExtendedDrawSchema implements DrawSchema {
                    id: Int!
                    info: DrawSchemaInfo!
                    extendedInfo: DrawSchemaInfo!
                }
                type Product {
                    id: Int!
                    schemas: [DrawSchema]
                }
                type Query {
                    products: [Product]
                }",
                cfg =>
                {
                    cfg.Types.For("DrawSchema").ResolveType = _ => throw new NotImplementedException();
                });

            var query = new Product[0]
                .AsQueryable()
                .Include(p => p.Schemas.Include(s => s.Info))
                .Include(p => p.Schemas.Include(s => ((ExtendedDrawSchema)s).ExtendedInfo));

            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal(
                @"query { products { id schemas { __typename id info { author } ... on ExtendedDrawSchema { extendedInfo { author } } } } }",
                queryText);
        }

        [Fact]
        public void ScalarCollectionFetchTest()
        {
            var queryBuilder = GetQueryBuilder(@"
                type Product {
                    remarks: [String]
                }
                type Query {
                    products: [Product]
                }");

            var query = new Product[0].AsQueryable();
            var queryText = queryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out _);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query { products { remarks } }", queryText);
        }

        private GqlQueryBuilder GetQueryBuilder(string typeDefinitions, Action<SchemaBuilder> configure = null)
        {
            return new GqlQueryBuilder(Schema.For(typeDefinitions, configure), mapper);
        }

        private string PostProcessQuery(string query)
        {
            query = Regex.Replace(query, @"{", " { ");
            query = Regex.Replace(query, @"}", " } ");
            query = Regex.Replace(query, @"{{", "{ {");
            query = Regex.Replace(query, @"}}", "} }");
            query = Regex.Replace(query, @"\r", " ");
            query = Regex.Replace(query, @"\n", " ");
            query = Regex.Replace(query, @"\s\s+", " ");
            return query.Trim();
        }

        private class Product
        {
            public int Id { get; }

            public ProductInfo ProductInfo { get; }

            public IEnumerable<DrawSchema> Schemas { get; }
        }

        private class ProductInfo
        {
            public ProductAbout About { get; }
        }

        private class ProductAbout
        {
        }

        private class DrawSchema
        {
            public DrawSchemaInfo Info { get; }
        }

        private class DrawSchemaInfo
        {
        }

        private class ExtendedDrawSchema : DrawSchema
        {
            public DrawSchemaInfo ExtendedInfo { get; }
        }

        private class TypeTemplate
        {
        }

        private class TextboxTemplate : TypeTemplate
        {
            public TextboxFrame Frame { get; }
        }

        private class TextboxFrame
        {
        }
    }
}

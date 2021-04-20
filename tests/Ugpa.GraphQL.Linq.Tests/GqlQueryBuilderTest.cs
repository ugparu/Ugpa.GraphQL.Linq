using System.Linq;
using System.Text.RegularExpressions;
using Ugpa.GraphQL.Linq.Tests.Fixtures;
using Xunit;

using static Ugpa.GraphQL.Linq.Tests.Fixtures.GqlSchemaFixture;

namespace Ugpa.GraphQL.Linq.Tests
{
    public class GqlQueryBuilderTest : IClassFixture<GqlSchemaFixture>
    {
        private readonly GqlQueryProvider provider;

        public GqlQueryBuilderTest(GqlSchemaFixture schemaFixture)
        {
            provider = new GqlQueryProvider(null, schemaFixture.Schema);
        }

        [Fact]
        public void SimpleQueryTest()
        {
            var query = new GqlQueryable<Product>(provider);

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { id name comment } }", queryText);
        }

        [Fact]
        public void SingleSelectQueryTest()
        {
            var query = new GqlQueryable<Product>(provider)
                .Select(_ => _.ProductInfo);

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { productInfo { title version } } }", queryText);
        }

        [Fact]
        public void NestedPropertySelectQueryTest()
        {
            var query = new GqlQueryable<Product>(provider)
                .Select(_ => _.ProductInfo.About);

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { productInfo { about { stamp } } } }", queryText);
        }

        [Fact]
        public void ChainedSelectQueryTest()
        {
            var query = new GqlQueryable<Product>(provider)
                .Select(_ => _.ProductInfo)
                .Select(_ => _.About);

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { productInfo { about { stamp } } } }", queryText);
        }

        [Fact]
        public void SelectManyQueryTest()
        {
            var query = new GqlQueryable<Product>(provider)
                .SelectMany(_ => _.Schemas);

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { schemas { id name } } }", queryText);
        }

        [Fact]
        public void SingleIncludeQueryTest()
        {
            var query = new GqlQueryable<Product>(provider)
                .Include(_ => _.ProductInfo);

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { id name comment productInfo { title version } } }", queryText);
        }

        [Fact]
        public void NestedIncludeQueryTest()
        {
            var query = new GqlQueryable<Product>(provider)
                .Include(_ => _.ProductInfo.About);

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { id name comment productInfo { about { stamp } } } }", queryText);
        }

        [Fact]
        public void MultipleIncludesQueryTest()
        {
            var query = new GqlQueryable<Product>(provider)
                .Include(_ => _.ProductInfo)
                .Include(_ => _.ProductInfo.About);

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("products", entryPoint);
            Assert.Equal("query { products { id name comment productInfo { title version about { stamp } } } }", queryText);
        }

        [Fact]
        public void SimpleParametrizedQueryTest()
        {
            var query = new GqlQueryable<DrawSchema>(provider)
                .Where(new { productId = 111 });

            var variablesResolver = new VariablesResolver();
            var queryText = GqlQueryBuilder.BuildQuery(query.Expression, variablesResolver, out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("schemas", entryPoint);
            Assert.Equal("query($linq_param_0: Int!) { schemas(productId: $linq_param_0) { id name } }", queryText);

            var variable = Assert.Single(variablesResolver.GetAllVariables());
            Assert.Equal("linq_param_0", variable.name);
            Assert.Equal(111, variable.value);
        }

        [Fact]
        public void ParametrizedSelectQueryTest()
        {
            var query = new GqlQueryable<Product>(provider)
                .Where(new { productId = 111 })
                .Select(_ => _.ProductInfo);

            var variablesResolver = new VariablesResolver();
            var queryText = GqlQueryBuilder.BuildQuery(query.Expression, variablesResolver, out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("product", entryPoint);
            Assert.Equal("query($linq_param_0: Int!) { product(productId: $linq_param_0) { productInfo { title version } } }", queryText);

            var variable = Assert.Single(variablesResolver.GetAllVariables());
            Assert.Equal("linq_param_0", variable.name);
            Assert.Equal(111, variable.value);
        }

        [Fact]
        public void InterfaceImplementationQueryTest()
        {
            var query = new GqlQueryable<TypeTemplate>(provider);

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression, new VariablesResolver(), out var entryPoint);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("templates", entryPoint);
            Assert.Equal(
                "query { templates { __typename id name ... on TextTemplate { fontFamily fontSize } ... on RailchainTemplate { mainLineWidth sideLineWidth } } }",
                queryText);
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
    }
}

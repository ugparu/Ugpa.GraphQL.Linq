using System.Linq;
using System.Text.RegularExpressions;
using Ugpa.GraphQL.Linq.Tests.Fixtures;
using Xunit;

namespace Ugpa.GraphQL.Linq.Tests
{
    public class GqlQueryBuilderTest : IClassFixture<GqlQueryProviderFixture>
    {
        private readonly GqlQueryProvider provider;

        public GqlQueryBuilderTest(GqlQueryProviderFixture providerFixture)
        {
            provider = providerFixture.Provider;
        }

        [Fact]
        public void SimpleQueryTest()
        {
            var query = new GqlQueryable<Product>(provider, "products");

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query { products { id name comment } }", queryText);
        }

        [Fact]
        public void SingleSelectQueryTest()
        {
            var query = new GqlQueryable<Product>(provider, "products")
                .Select(_ => _.ProductInfo);

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query { products { productInfo { title version } } }", queryText);
        }

        [Fact]
        public void NestedPropertySelectQueryTest()
        {
            var query = new GqlQueryable<Product>(provider, "products")
                .Select(_ => _.ProductInfo.About);

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query { products { productInfo { about { stamp } } } }", queryText);
        }

        [Fact]
        public void ChainedSelectQueryTest()
        {
            var query = new GqlQueryable<Product>(provider, "products")
                .Select(_ => _.ProductInfo)
                .Select(_ => _.About);

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query { products { productInfo { about { stamp } } } }", queryText);
        }

        [Fact]
        public void SelectManyQueryTest()
        {
            var query = new GqlQueryable<Product>(provider, "products")
                .SelectMany(_ => _.Schemas);

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query { products { schemas { id name } } }", queryText);
        }


        [Fact]
        public void SimpleParametrizedQueryTest()
        {
            var query = new GqlQueryable<DrawSchema>(provider, "schemas")
                .Where(new { productId = 111 });

            var queryText = GqlQueryBuilder.BuildQuery(query.Expression);
            queryText = PostProcessQuery(queryText);

            Assert.Equal("query($linq_param_0: Int!) { schemas(productId: $linq_param_0) { id name } }", queryText);
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

        private sealed class Product
        {
            public ProductInfo ProductInfo { get; set; }

            public DrawSchema[] Schemas { get; set; }
        }

        private sealed class ProductInfo
        {
            public ProductAbout About { get; set; }
        }

        private sealed class ProductAbout
        {
        }

        private sealed class DrawSchema
        {
        }
    }
}

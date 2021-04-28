using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL.Client.Abstractions;
using GraphQL.Types;
using Moq;
using Ugpa.GraphQL.Linq.Tests.Fixtures;
using Ugpa.GraphQL.Linq.Utils;
using Xunit;

namespace Ugpa.GraphQL.Linq.Tests
{
    public sealed class GqlQueryProviderTest : IClassFixture<GqlClientFixture>
    {
        private readonly GqlQueryBuilder queryBuilder;
        private readonly IGraphQLClient client;

        public GqlQueryProviderTest(GqlClientFixture clientFixture)
        {
            var mapper = new Mock<IGraphTypeNameMapper>();
            mapper.Setup(_ => _.GetTypeName(It.IsAny<Type>())).Returns((Type t) => t.Name);

            var schema = Schema.For(@"
                type Object {
                    id: Int!
                }
                type Query {
                    objects: [Object]
                }");

            queryBuilder = new GqlQueryBuilder(schema, mapper.Object);

            client = clientFixture.CreateClientFor(
                schema,
                new
                {
                    objects = new[]
                    {
                        new { id = 1 },
                        new { id = 2 },
                        new { id = 3 }
                    }
                });
        }

        [Fact]
        public void GenericCreateQueryTest()
        {
            var provider = new GqlQueryProvider(client, queryBuilder);
            var sourceQuery = new object[0].AsQueryable();
            Assert.NotNull(provider.CreateQuery<object>(sourceQuery.Expression));
        }

        [Fact]
        public void GenericExecuteTest()
        {
            var provider = new GqlQueryProvider(client, queryBuilder);
            var query = new object[0].AsQueryable();
            var result = provider.Execute<IEnumerable<object>>(query.Expression);
            Assert.IsAssignableFrom<IEnumerable<object>>(result);
            Assert.Equal(3, result.Count());
        }

        [Fact]
        public void NonGenericCreateQueryTest()
        {
            var provider = new GqlQueryProvider(client, queryBuilder);
            var sourceQuery = new object[0].AsQueryable();
            Assert.NotNull(provider.CreateQuery(sourceQuery.Expression));
        }

        [Fact]
        public void NonGenericExecuteTest()
        {
            var provider = new GqlQueryProvider(client, queryBuilder);
            var query = new object[0].AsQueryable();
            var result = provider.Execute(query.Expression);
            var pp = Assert.IsAssignableFrom<IEnumerable<object>>(result);
            Assert.Equal(3, pp.Count());
        }
    }
}

﻿using System.Collections.Generic;
using System.Linq;
using GraphQL.Client.Abstractions;
using GraphQL.Types;
using Ugpa.GraphQL.Linq.Tests.Fixtures;
using Xunit;
using static Ugpa.GraphQL.Linq.Tests.Fixtures.GqlSchemaFixture;

namespace Ugpa.GraphQL.Linq.Tests
{
    public sealed class GqlQueryProviderTest : IClassFixture<GqlSchemaFixture>, IClassFixture<GqlClientFixture>
    {
        private readonly ISchema schema;
        private readonly IGraphQLClient client;

        public GqlQueryProviderTest(GqlSchemaFixture schemaFixture, GqlClientFixture clientFixture)
        {
            schema = schemaFixture.Schema;
            client = clientFixture.CreateClientFor(
                schemaFixture.Schema,
                new
                {
                    products = new[]
                    {
                        new { id = 1, name = "1", comment = "1" },
                        new { id = 2, name = "2", comment = "2" },
                        new { id = 3, name = "3", comment = "3" }
                    }
                });
        }

        [Fact]
        public void GenericCreateQueryTest()
        {
            var provider = new GqlQueryProvider(client, schema);
            var sourceQuery = new GqlQueryable<Product>(provider);
            provider.CreateQuery<Product>(sourceQuery.Expression);
        }

        [Fact]
        public void GenericExecuteTest()
        {
            var provider = new GqlQueryProvider(client, schema);
            var query = new GqlQueryable<Product>(provider);
            var result = provider.Execute<IEnumerable<Product>>(query.Expression);
            Assert.IsAssignableFrom<IEnumerable<Product>>(result);
            Assert.Equal(3, result.Count());
        }

        [Fact]
        public void NonGenericCreateQueryTest()
        {
            var provider = new GqlQueryProvider(client, schema);
            var sourceQuery = new GqlQueryable<Product>(provider);
            var query = provider.CreateQuery(sourceQuery.Expression);
        }

        [Fact]
        public void NonGenericExecuteTest()
        {
            var provider = new GqlQueryProvider(client, schema);
            var query = new GqlQueryable<Product>(provider);
            var result = provider.Execute(query.Expression);
            var pp = Assert.IsAssignableFrom<IEnumerable<Product>>(result);
            Assert.Equal(3, pp.Count());
        }
    }
}
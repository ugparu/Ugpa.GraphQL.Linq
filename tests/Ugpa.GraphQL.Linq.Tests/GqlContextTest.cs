using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;
using Moq;
using Ugpa.GraphQL.Linq.Tests.Fixtures;
using Xunit;

namespace Ugpa.GraphQL.Linq.Tests
{
    public sealed class GqlContextTest : IClassFixture<GqlClientFixture>
    {
        private readonly GqlClientFixture clientFixture;

        public GqlContextTest(GqlClientFixture clientFixture)
        {
            this.clientFixture = clientFixture;
        }

        [Fact]
        public void GqlContextQueryResultCachingTest()
        {
            var schema = Schema.For(@"
                type DrawSchema {
                    items: [DrawSchemaItem]
                }
                type DrawSchemaItem {
                    template: DrawSchemaItemTemplate!
                }
                type DrawSchemaItemTemplate {
                    id: ID
                }
                type Query {
                    schemas: [DrawSchema]
                    templates: [DrawSchemaItemTemplate]
                }");

            var root = new
            {
                schemas = new[]
                {
                    new
                    {
                        items = new []
                        {
                            new { template = new { id = 0 } },
                            new { template = new { id = 0 } },
                            new { template = new { id = 1 } }
                        }
                    }
                },
                templates = new[]
                {
                    new { id = 0 },
                    new { id = 1 }
                }
            };

            var context = new GqlContext(
                () => clientFixture.CreateClientFor(schema, root),
                Mock.Of<ISchemaSource>(_ => _.GetSchema() == schema));

            var templates = context.Get<DrawSchemaItemTemplate>().ToArray();
            var items = context.Get<DrawSchema>()
                .Include(s => s.Items.Include(i => i.Template))
                .ToArray()[0]
                .Items
                .ToArray();

            Assert.Same(items[0].Template, items[1].Template);
            Assert.Same(templates[0], items[0].Template);
            Assert.Same(templates[1], items[2].Template);
        }

        [Fact]
        public void CollectionRepopulatingTest()
        {
            var schema = Schema.For(@"
                type DrawSchema {
                    id: ID!
                    items: [DrawSchemaItem]
                }
                type DrawSchemaItem {
                    name: String!
                }
                type Query {
                    schema: DrawSchema
                }");

            var root = new
            {
                schema = new
                {
                    id = 123,
                    items = new[]
                    {
                        new { name = "i1" },
                        new { name = "i2" }
                    }
                }
            };

            var context = new GqlContext(
                () => clientFixture.CreateClientFor(schema, root),
                Mock.Of<ISchemaSource>(_ => _.GetSchema() == schema));

            var drawSchema = context.Get<DrawSchema>().Include(s => s.Items).ToArray()[0];
            Assert.Equal(2, drawSchema.Items.Count());

            context.Get<DrawSchema>().Include(s => s.Items).ToArray();
            Assert.Equal(2, drawSchema.Items.Count());
        }

        private class DrawSchema
        {
            public IEnumerable<DrawSchemaItem> Items { get; } = new List<DrawSchemaItem>();
        }

        private class DrawSchemaItem
        {
            public DrawSchemaItemTemplate Template { get; set; }
        }

        private class DrawSchemaItemTemplate
        {
        }
    }
}

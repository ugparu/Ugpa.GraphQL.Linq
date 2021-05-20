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
            var schemas = context.Get<DrawSchema>().Include(s => s.Items.Include(i => i.Template)).ToArray();

            Assert.Same(schemas[0].Items[0].Template, schemas[0].Items[1].Template);
            Assert.Same(templates[0], schemas[0].Items[0].Template);
            Assert.Same(templates[1], schemas[0].Items[2].Template);
        }

        private class DrawSchema
        {
            public DrawSchemaItem[] Items { get; set; }
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

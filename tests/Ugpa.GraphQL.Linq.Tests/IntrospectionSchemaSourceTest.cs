using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Types;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ugpa.GraphQL.Linq.Tests
{
    public sealed class IntrospectionSchemaSourceTest
    {
        [Fact]
        public void EmtyQueryTypeTest()
        {
            var data = @"
{ ""__schema"": {
    ""queryType"": { ""name"": ""QT"" },
    ""types"": [
    { ""name"": ""QT"", ""kind"": ""OBJECT"", ""fields"": [], ""interfaces"": [] }
    ]
}}";

            var source = new IntrospectionSchemaSource(new DummyClient(data));
            var schema = source.GetSchema();

            Assert.NotNull(schema.Query);
            Assert.Equal("QT", schema.Query.Name);
        }

        [Fact]
        public void ScalarFieldsResolveTest()
        {
            var data = @"
{ ""__schema"": {
    ""queryType"": { ""name"": ""QT"" },
    ""types"": [
    {
        ""name"": ""QT"",
        ""kind"": ""OBJECT"",
        ""fields"": [
        { ""name"": ""idField"", ""type"": { ""name"": ""ID"" }, ""args"": [] },
        { ""name"": ""intField"", ""type"": { ""name"":  ""Int"" }, ""args"": [] },
        { ""name"": ""floatField"", ""type"": { ""name"":  ""Float"" }, ""args"": [] },
        { ""name"": ""stringField"", ""type"": { ""name"":  ""String"" }, ""args"": [] }
        ],
        ""interfaces"": [] },
    { ""name"": ""ID"", ""kind"": ""SCALAR"" },
    { ""name"": ""Int"", ""kind"": ""SCALAR"" },
    { ""name"": ""Float"", ""kind"": ""SCALAR"" }
    ]
}}";
            var source = new IntrospectionSchemaSource(new DummyClient(data));
            var schema = source.GetSchema();

            Assert.Equal(4, schema.Query.Fields.Count());
            Assert.IsAssignableFrom<IdGraphType>(schema.Query.GetField("idField").ResolvedType);
            Assert.IsAssignableFrom<IntGraphType>(schema.Query.GetField("intField").ResolvedType);
            Assert.IsAssignableFrom<FloatGraphType>(schema.Query.GetField("floatField").ResolvedType);
            Assert.IsAssignableFrom<StringGraphType>(schema.Query.GetField("stringField").ResolvedType);
        }

        [Fact]
        public void NonNullTypeResolveTest()
        {
            var data = @"
{ ""__schema"": {
    ""queryType"": { ""name"": ""QT"" },
    ""types"": [
    {
        ""name"": ""QT"",
        ""kind"": ""OBJECT"",
        ""fields"": [
        { ""name"": ""nonNullField"", ""type"": { ""name"": null, ""kind"": ""NON_NULL"", ""ofType"": { ""name"": ""Float"" } }, ""args"": [] }
        ],
        ""interfaces"": [] },
    { ""name"": ""Float"", ""kind"": ""SCALAR"" }
    ]
}}";

            var source = new IntrospectionSchemaSource(new DummyClient(data));
            var schema = source.GetSchema();

            var nn = Assert.IsAssignableFrom<NonNullGraphType>(schema.Query.GetField("nonNullField").ResolvedType);
            Assert.IsAssignableFrom<FloatGraphType>(nn.ResolvedType);
        }

        [Fact]
        public void ListTypeResolveTest()
        {
            var data = @"
{ ""__schema"": {
    ""queryType"": { ""name"": ""QT"" },
    ""types"": [
    {
        ""name"": ""QT"",
        ""kind"": ""OBJECT"",
        ""fields"": [
        { ""name"": ""listField"", ""type"": { ""name"": null, ""kind"": ""LIST"", ""ofType"": { ""name"": ""Float"" } }, ""args"": [] }
        ],
        ""interfaces"": [] },
    { ""name"": ""Float"", ""kind"": ""SCALAR"" }
    ]
}}";

            var source = new IntrospectionSchemaSource(new DummyClient(data));
            var schema = source.GetSchema();

            var nn = Assert.IsAssignableFrom<ListGraphType>(schema.Query.GetField("listField").ResolvedType);
            Assert.IsAssignableFrom<FloatGraphType>(nn.ResolvedType);
        }

        [Fact]
        public void NonNullListTypeResolveTest()
        {
            var data = @"
{ ""__schema"": {
    ""queryType"": { ""name"": ""QT"" },
    ""types"": [
    {
        ""name"": ""QT"",
        ""kind"": ""OBJECT"",
        ""fields"": [
        {
            ""name"": ""nonNullListField"",
            ""type"": {
                ""name"": null,
                ""kind"": ""NON_NULL"",
                ""ofType"": {
                    ""name"": null,
                    ""kind"": ""LIST"",
                    ""ofType"": { ""name"": ""Float"" } } },
            ""args"": [] }
        ],
        ""interfaces"": [] },
    { ""name"": ""Float"", ""kind"": ""SCALAR"" }
    ]
}}";

            var source = new IntrospectionSchemaSource(new DummyClient(data));
            var schema = source.GetSchema();

            var nn = Assert.IsAssignableFrom<NonNullGraphType>(schema.Query.GetField("nonNullListField").ResolvedType);
            var ll = Assert.IsAssignableFrom<ListGraphType>(nn.ResolvedType);
            Assert.IsAssignableFrom<FloatGraphType>(ll.ResolvedType);
        }

        private sealed class DummyClient : IGraphQLClient
        {
            private readonly JObject data;

            public DummyClient(string data)
                : this(JObject.Parse(data))
            {
            }

            public DummyClient(JObject data)
            {
                this.data = data;
            }

            public IObservable<GraphQLResponse<TResponse>> CreateSubscriptionStream<TResponse>(GraphQLRequest request)
                => throw new NotImplementedException();

            public IObservable<GraphQLResponse<TResponse>> CreateSubscriptionStream<TResponse>(GraphQLRequest request, Action<Exception> exceptionHandler)
                => throw new NotImplementedException();

            public void Dispose()
                => throw new NotImplementedException();

            public Task<GraphQLResponse<TResponse>> SendMutationAsync<TResponse>(GraphQLRequest request, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task<GraphQLResponse<TResponse>> SendQueryAsync<TResponse>(GraphQLRequest request, CancellationToken cancellationToken = default)
                => Task.FromResult(new GraphQLResponse<TResponse> { Data = (TResponse)(object)data });
        }
    }
}

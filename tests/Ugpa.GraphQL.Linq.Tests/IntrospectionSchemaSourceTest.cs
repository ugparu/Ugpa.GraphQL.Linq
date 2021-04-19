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
        public void EmptyQueryTypeTest()
        {
            var data = @"
{ ""__schema"": {
    ""queryType"": { ""name"": ""QT"" },
    ""types"": [
    { ""name"": ""QT"", ""kind"": ""OBJECT"", ""fields"": [], ""interfaces"": [] }
    ]
}}";

            var source = new IntrospectionSchemaSource(new DummyClient(data));
            Assert.Throws<InvalidOperationException>(() => source.GetSchema());
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

        [Fact]
        public void InterfaceImplementationResolveTest()
        {
            var data = @"
{ ""__schema"": {
    ""queryType"": { ""name"": ""QT"", ""fields"": [ { ""name"": ""foo"", ""type"": { ""name"": ""FooInterface"" } } ] },
    ""types"": [
    {
        ""name"": ""QT"",
        ""kind"": ""OBJECT"",
        ""fields"": [
            { ""name"": ""foo"", ""type"": { ""name"": ""FooInterface"" }, ""args"": [] }
        ],
        ""interfaces"": [] },
    {
        ""name"": ""FooInterface"",
        ""kind"": ""INTERFACE"",
        ""fields"": [ { ""name"": ""X"", ""type"": { ""name"": ""String"" }, ""args"": [] } ] },
    {
        ""name"": ""FooImplA"",
        ""kind"": ""OBJECT"",
        ""fields"": [
            { ""name"": ""A"", ""type"": { ""name"": ""String"" }, ""args"": [] },
            { ""name"": ""X"", ""type"": { ""name"": ""String"" }, ""args"": [] } ],
        ""interfaces"": [ { ""name"": ""FooInterface"" } ] },
    {
        ""name"": ""FooImplB"",
        ""kind"": ""OBJECT"",
        ""fields"": [
            { ""name"": ""B"", ""type"": { ""name"": ""String"" }, ""args"": [] },
            { ""name"": ""X"", ""type"": { ""name"": ""String"" }, ""args"": [] } ],
        ""interfaces"": [ { ""name"": ""FooInterface"" } ] }
    ]
}}";
            var source = new IntrospectionSchemaSource(new DummyClient(data));
            var schema = source.GetSchema();

            var i = Assert.IsAssignableFrom<InterfaceGraphType>(schema.Query.GetField("foo").ResolvedType);
            Assert.Equal(2, i.PossibleTypes.Count());

            var implA = Assert.IsAssignableFrom<ObjectGraphType>(schema.FindType("FooImplA"));
            var implB = Assert.IsAssignableFrom<ObjectGraphType>(schema.FindType("FooImplB"));

            Assert.Single(i.PossibleTypes, _ => _ == implA);
            Assert.Single(i.PossibleTypes, _ => _ == implB);

            Assert.Single(implA.ResolvedInterfaces, _ => _ == i);
            Assert.Single(implB.ResolvedInterfaces, _ => _ == i);
        }

        [Fact]
        public void InputObjectResolveTest()
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
                    ""name"": ""inputField"",
                    ""type"": { ""name"": ""Int"" },
                    ""args"": [
                    { ""name"": ""inputValueA"", ""type"": { ""name"": ""Int"" } },
                    { ""name"": ""inputValueB"", ""type"": { ""name"": ""InputFoo"" } } ] }
            ],
            ""interfaces"": [] },
        {
            ""name"": ""InputFoo"",
            ""kind"": ""INPUT_OBJECT"",
            ""inputFields"": [
                { ""name"": ""idField"", ""type"": { ""name"": ""ID"" } },
                { ""name"": ""intField"", ""type"": { ""name"": ""Int"" } },
                { ""name"": ""nonNullIntField"", ""type"": { ""name"": null, ""kind"": ""NON_NULL"", ""ofType"": { ""name"": ""Int"" } } }
            ]
        },
        { ""name"": ""ID"", ""kind"": ""SCALAR"" },
        { ""name"": ""Int"", ""kind"": ""SCALAR"" }
    ]
}}";

            var source = new IntrospectionSchemaSource(new DummyClient(data));
            var schema = source.GetSchema();

            var f = schema.Query.GetField("inputField");
            Assert.Equal(2, f.Arguments.Count);
            Assert.IsAssignableFrom<IntGraphType>(f.Arguments.Find("inputValueA").ResolvedType);

            var it = Assert.IsAssignableFrom<InputObjectGraphType>(f.Arguments.Find("inputValueB").ResolvedType);
            Assert.Equal(3, it.Fields.Count());
            Assert.IsAssignableFrom<IdGraphType>(it.GetField("idField").ResolvedType);
            Assert.IsAssignableFrom<IntGraphType>(it.GetField("intField").ResolvedType);
            var nn = Assert.IsAssignableFrom<NonNullGraphType>(it.GetField("nonNullIntField").ResolvedType);
            Assert.IsAssignableFrom<IntGraphType>(nn.ResolvedType);
        }

        [Fact]
        public void EnumerationResolveTest()
        {
            var data = @"
{ ""__schema"": {
    ""queryType"": { ""name"": ""QT"" },
    ""types"": [
        {
            ""name"": ""QT"",
            ""kind"": ""OBJECT"",
            ""fields"": [ { ""name"": ""enumField"", ""type"": { ""name"": ""enumType"" }, ""args"": [] } ],
            ""interfaces"": []
        },
        {
            ""name"": ""enumType"",
            ""kind"": ""ENUM"",
            ""enumValues"": [ { ""name"": ""FOO"" }, { ""name"": ""BAR"" } ]
        }
    ]
}}";

            var source = new IntrospectionSchemaSource(new DummyClient(data));
            var schema = source.GetSchema();

            var en = Assert.IsAssignableFrom<EnumerationGraphType>(schema.Query.GetField("enumField").ResolvedType);
            Assert.Equal(2, en.Values.Count);
            Assert.Equal("FOO", en.Values["FOO"].Name);
            Assert.Equal("BAR", en.Values["BAR"].Name);
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

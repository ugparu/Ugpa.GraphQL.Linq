using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Serializer.Newtonsoft;
using GraphQL.Types;
using Newtonsoft.Json.Linq;
using Ugpa.GraphQL.Linq.Tests.Fixtures;
using Xunit;

namespace Ugpa.GraphQL.Linq.Tests
{
    public sealed class IntrospectionSchemaSourceTest : IClassFixture<GqlClientFixture>
    {
        private readonly GqlClientFixture clientFixture;

        public IntrospectionSchemaSourceTest(GqlClientFixture clientFixture)
        {
            this.clientFixture = clientFixture;
        }

        [Fact]
        public void EmptyQueryTypeTest()
        {
            var client = clientFixture.CreateClientFor(@"type Query { }");
            var source = new IntrospectionSchemaSource(client);
            Assert.Throws<InvalidOperationException>(() => source.GetSchema());
        }

        [Fact]
        public void ScalarFieldsResolveTest()
        {
            var client = clientFixture.CreateClientFor(@"
                type Query {
                    idField: ID
                    intField: Int
                    floatField: Float
                    stringField: String
                }");

            var source = new IntrospectionSchemaSource(client);
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
            var client = clientFixture.CreateClientFor(@"type Query { nonNullField: Float! }");
            var source = new IntrospectionSchemaSource(client);
            var schema = source.GetSchema();

            var nn = Assert.IsAssignableFrom<NonNullGraphType>(schema.Query.GetField("nonNullField").ResolvedType);
            Assert.IsAssignableFrom<FloatGraphType>(nn.ResolvedType);
        }

        [Fact]
        public void ListTypeResolveTest()
        {
            var client = clientFixture.CreateClientFor(@"type Query { listField: [Float] }");
            var source = new IntrospectionSchemaSource(client);
            var schema = source.GetSchema();

            var nn = Assert.IsAssignableFrom<ListGraphType>(schema.Query.GetField("listField").ResolvedType);
            Assert.IsAssignableFrom<FloatGraphType>(nn.ResolvedType);
        }

        [Fact]
        public void NonNullListTypeResolveTest()
        {
            var client = clientFixture.CreateClientFor(@"type Query { nonNullListField: [Float]! }");
            var source = new IntrospectionSchemaSource(client);
            var schema = source.GetSchema();

            var nn = Assert.IsAssignableFrom<NonNullGraphType>(schema.Query.GetField("nonNullListField").ResolvedType);
            var ll = Assert.IsAssignableFrom<ListGraphType>(nn.ResolvedType);
            Assert.IsAssignableFrom<FloatGraphType>(ll.ResolvedType);
        }

        [Fact]
        public void InterfaceImplementationResolveTest()
        {
            var client = clientFixture.CreateClientFor(
                @"
                interface FooInterface {
                    X: String
                }
                type FooImplA implements FooInterface {
                    A: String
                    X: String
                }
                type FooImplB implements FooInterface {
                    B: String
                    X: String
                }
                type Query {
                    foo: FooInterface
                }",
                configure: _ =>
                {
                    _.Types.For("FooInterface").ResolveType = obj => throw new NotSupportedException();
                });

            var source = new IntrospectionSchemaSource(client);
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
            var client = clientFixture.CreateClientFor(@"
                input InputFoo {
                    idField: ID
                    intField: Int
                    nonNullIntField: Int!
                }
                type Query {
                    inputField(inputValueA: Int, inputValueB: InputFoo): Int
                }");

            var source = new IntrospectionSchemaSource(client);
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
            var client = clientFixture.CreateClientFor(@"
                enum EnumType { FOO BAR }
                type Query{ enumField: EnumType }");

            var source = new IntrospectionSchemaSource(client);
            var schema = source.GetSchema();

            var en = Assert.IsAssignableFrom<EnumerationGraphType>(schema.Query.GetField("enumField").ResolvedType);
            Assert.Equal(2, en.Values.Count());
            Assert.Equal("FOO", en.Values["FOO"].Name);
            Assert.Equal("BAR", en.Values["BAR"].Name);
        }
    }
}

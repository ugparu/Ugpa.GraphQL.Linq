using System;
using System.Linq;
using GraphQL.Types;
using GraphQL.Utilities;
using Moq;
using Newtonsoft.Json.Linq;
using Ugpa.GraphQL.Linq.Utils;
using Xunit;

namespace Ugpa.GraphQL.Linq.Tests
{
    public sealed class EntityCacheTest
    {
        [Fact]
        public void NoIdExtractTest()
        {
            var cache = CreateEntityCache("type Object { id: String! }");

            var jObj = (JObject)JToken.Parse(@"{ }");

            var obj = cache.GetEntity(jObj, typeof(object), out var id);
            Assert.Null(obj);
            Assert.Null(id);
        }

        [Fact]
        public void SimpleIdExtractTest()
        {
            var cache = CreateEntityCache("type Object { id: ID }");

            var jObj = (JObject)JToken.Parse(@"{""id"": ""123""}");

            var obj = cache.GetEntity(jObj, typeof(object), out var id);
            Assert.Null(obj);
            Assert.Equal("123", id);
        }

        [Fact]
        public void ObjectsAreCachedByTypes()
        {
            var cache = CreateEntityCache(@"
                type Int32 { id: ID }
                type Int64 { id: ID }");

            cache.PutEntity("123", 123);
            cache.PutEntity("123", (long)456);

            var jObj = (JObject)JToken.Parse(@"{ ""id"": ""123"" }");

            Assert.Equal(123, cache.GetEntity(jObj, typeof(int), out _));
            Assert.Equal((long)456, cache.GetEntity(jObj, typeof(long), out _));
        }

        [Fact]
        public void ImplementationsCachedAsInterfacesTest()
        {
            var cache = CreateEntityCache(
                @"interface Foo { id: ID }
                type FooA implements Foo { id: ID }
                type FooB implements Foo { id: ID }",
                cfg =>
                {
                    cfg.Types.For("Foo").ResolveType = _ => throw new NotImplementedException();
                });

            cache.PutEntity("123", new FooA());
            var jObjA = (JObject)JToken.Parse(@"{ ""id"": ""123"" }");
            var objA = cache.GetEntity(jObjA, typeof(FooA), out _);
            var fooA = cache.GetEntity(jObjA, typeof(Foo), out _);
            Assert.Same(objA, fooA);

            cache.PutEntity("456", new FooB());
            var jObjB = (JObject)JToken.Parse(@"{ ""id"": ""456"" }");
            var objB = cache.GetEntity(jObjB, typeof(FooB), out _);
            var fooB = cache.GetEntity(jObjB, typeof(Foo), out _);
            Assert.Same(objB, fooB);
        }

        [Fact]
        public void ErrorOnDuplicatePutTest()
        {
            var cache = CreateEntityCache(@"type Object { id: ID }");
            var obj = new object();
            cache.PutEntity("123", obj);
            Assert.Throws<InvalidOperationException>(() => cache.PutEntity("123", obj));
        }

        private EntityCache CreateEntityCache(string typeDefinitions, Action<SchemaBuilder> configure = null)
        {
            var schema = Schema.For(typeDefinitions, configure);

            var mapper = new Mock<IGraphTypeMapper>();
            mapper
                .Setup(_ => _.GetGraphType(It.IsAny<Type>()))
                .Returns((Type t) => schema.AllTypes.First(_ => _.Name == t.Name));

            return new EntityCache(mapper.Object);
        }

        private abstract class Foo
        {
        }

        private sealed class FooA : Foo
        {
        }

        private sealed class FooB : Foo
        {
        }
    }
}

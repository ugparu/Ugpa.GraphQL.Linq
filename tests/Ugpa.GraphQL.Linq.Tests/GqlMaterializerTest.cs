using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GraphQL.Types;
using GraphQL.Utilities;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Ugpa.GraphQL.Linq.Utils;
using Xunit;

namespace Ugpa.GraphQL.Linq.Tests
{
    public sealed class GqlMaterializerTest
    {
        [Theory]
        [InlineData(typeof(bool), false)]
        [InlineData(typeof(byte), false)]
        [InlineData(typeof(sbyte), false)]
        [InlineData(typeof(short), false)]
        [InlineData(typeof(ushort), false)]
        [InlineData(typeof(int), false)]
        [InlineData(typeof(int?), false)]
        [InlineData(typeof(uint), false)]
        [InlineData(typeof(long), false)]
        [InlineData(typeof(ulong), false)]
        [InlineData(typeof(float), false)]
        [InlineData(typeof(double), false)]
        [InlineData(typeof(decimal), false)]
        [InlineData(typeof(string), false)]
        [InlineData(typeof(Array), false)]
        [InlineData(typeof(object[]), false)]
        [InlineData(typeof(List<int>), false)]
        [InlineData(typeof(List<object>), false)]
        [InlineData(typeof(object), true)]
        public void CanConvertTest(Type objectType, bool expectedCanConvert)
        {
            var materializer = CreateMaterializer();
            var canConvert = materializer.CanConvert(objectType);
            Assert.Equal(expectedCanConvert, canConvert);
        }

        [Theory]
        [InlineData(typeof(FooA), "type FooA { id: ID }")]
        [InlineData(typeof(FooB), "type FooB { id: ID }")]
        public void DefaultTypeResolvesCorrectlyTest(Type objectType, string typeDefinitions)
        {
            var materializer = CreateMaterializer(typeDefinitions);
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                ReferenceResolverProvider = () => Mock.Of<IReferenceResolver>()
            });
            var reader = new JsonTextReader(new StringReader("{}"));
            var obj = materializer.ReadJson(reader, objectType, null, serializer);
            Assert.Equal(objectType, obj.GetType());
        }

        [Theory]
        [InlineData(typeof(IFoo))]
        [InlineData(typeof(Foo))]
        public void FailOnReadAbstractTypeTest(Type objectType)
        {
            var materializer = CreateMaterializer();
            var serializer = JsonSerializer.Create();
            var reader = new JsonTextReader(new StringReader("{}"));
            Assert.Throws<InvalidOperationException>(() => materializer.ReadJson(reader, objectType, null, serializer));
        }

        [Fact]
        public void FailOnReadMissingTypeTest()
        {
            var materializer = CreateMaterializer();
            var serializer = JsonSerializer.Create();
            var reader = new JsonTextReader(new StringReader(@"{ ""__typename"": ""Bar"" }"));
            Assert.Throws<InvalidOperationException>(() => materializer.ReadJson(reader, typeof(object), null, serializer));
        }

        [Fact]
        public void FailOnInheritanceHierarchyViolationTest()
        {
            var materializer = CreateMaterializer();
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                SerializationBinder = Mock.Of<ISerializationBinder>(_ => _.BindToType(null, "Bar") == typeof(Bar))
            });

            var reader = new JsonTextReader(new StringReader(@"{ ""__typename"": ""Bar"" }"));
            Assert.Throws<InvalidOperationException>(() => materializer.ReadJson(reader, typeof(Foo), null, serializer));
        }

        [Fact]
        public void MappedTypesReadCorrectlyTest()
        {
            var materializer = CreateMaterializer(@"
                type FooA { id: ID }
                type FooB { id: ID }");

            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                SerializationBinder = Mock.Of<ISerializationBinder>(_ =>
                    _.BindToType(null, "FooA") == typeof(FooA) &&
                    _.BindToType(null, "FooB") == typeof(FooB)),
                Converters =
                {
                    materializer
                },
                ReferenceResolverProvider = () => Mock.Of<IReferenceResolver>()
            });

            var reader = new JsonTextReader(new StringReader(@"
                [
                    { ""__typename"": ""FooA"", ""intValue"": 123 },
                    { ""__typename"": ""FooB"", ""stringValue"": ""ABC"" }
                ]"));

            var result = serializer.Deserialize<IFoo[]>(reader);
            var foos = Assert.IsAssignableFrom<IFoo[]>(result);
            Assert.Equal(2, foos.Length);
            var a = Assert.IsAssignableFrom<FooA>(foos[0]);
            var b = Assert.IsAssignableFrom<FooB>(foos[1]);
            Assert.Equal(123, a.IntValue);
            Assert.Equal("ABC", b.StringValue);
        }

        [Fact]
        public void ReadExistingObjectSuccessfullTest()
        {
            var materializer = CreateMaterializer();
            var serializer = JsonSerializer.Create();
            var reader = new JsonTextReader(new StringReader(@"{ ""intValue"": 123 }"));
            var existingObject = new FooA();
            var newObject = materializer.ReadJson(reader, typeof(FooA), existingObject, serializer);
            Assert.Same(existingObject, newObject);
            Assert.Equal(123, existingObject.IntValue);
        }

        [Fact]
        public void ReadNullValueTest()
        {
            var materializer = CreateMaterializer();
            var serializer = JsonSerializer.Create();
            var reader = new JsonTextReader(new StringReader(@"null"));
            var obj = materializer.ReadJson(reader, typeof(object), null, serializer);
            Assert.Null(obj);
        }

        [Fact]
        public void FailOnExistingObjectInheritanceViolationTest()
        {
            var materializer = CreateMaterializer();
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                SerializationBinder = Mock.Of<ISerializationBinder>(_ => _.BindToType(null, "FooA") == typeof(FooA))
            });

            var reader = new JsonTextReader(new StringReader(@"{ ""__typename"": ""FooA"", ""intValue"": 123 }"));
            var existingObject = new FooB();
            Assert.Throws<InvalidOperationException>(() => materializer.ReadJson(reader, typeof(FooB), existingObject, serializer));
        }

        [Fact]
        public void FailOnExistingObjectTypeMismatchTest()
        {
            var materializer = CreateMaterializer();
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                SerializationBinder = Mock.Of<ISerializationBinder>(_ => _.BindToType(null, "FooBB") == typeof(FooBB))
            });

            var reader = new JsonTextReader(new StringReader(@"{ ""__typename"": ""FooBB"", ""stringValue"": ""123"" }"));
            var existingObject = new FooB();
            Assert.Throws<InvalidOperationException>(() => materializer.ReadJson(reader, typeof(FooB), existingObject, serializer));
        }

        [Fact]
        public void ValuesWithIdAreCachedTest()
        {
            var materializer = CreateMaterializer("type Object { id: ID }");

            var serializer = JsonSerializer.Create();

            var json = @"{ ""id"": ""cachedObject1"" }";
            var o1 = materializer.ReadJson(new JsonTextReader(new StringReader(json)), typeof(object), null, serializer);
            var o2 = materializer.ReadJson(new JsonTextReader(new StringReader(json)), typeof(object), null, serializer);
            Assert.Same(o1, o2);
        }

        [Fact]
        public void ValuesWithNonNullIdAreCachedTest()
        {
            var materializer = CreateMaterializer("type Object { id: ID! }");

            var serializer = JsonSerializer.Create();

            var json = @"{ ""id"": ""cachedObject1"" }";
            var o1 = materializer.ReadJson(new JsonTextReader(new StringReader(json)), typeof(object), null, serializer);
            var o2 = materializer.ReadJson(new JsonTextReader(new StringReader(json)), typeof(object), null, serializer);
            Assert.Same(o1, o2);
        }

        [Fact]
        public void ReadingAbstractTypeWithCustomFactoryTest()
        {
            var materializer = CreateMaterializer("type IFoo { name: String! }");
            var contractResolver = new Ugpa.Json.Serialization.FluentContext();

            contractResolver.Configure<IFoo>(cfg => cfg.ConstructWith(() => new FooA()));

            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver = contractResolver
            });

            var json = @"{ ""name"": ""Foo 1"" }";
            var f = materializer.ReadJson(new JsonTextReader(new StringReader(json)), typeof(IFoo), null, serializer);
            Assert.IsType<FooA>(f);
        }

        [Fact]
        public void LoopedObjectCachingTest()
        {
            var materializer = CreateMaterializer(@"
                type LoopedObject {
                    id: ID
                    child: LoopedObject 
                }");

            var serializer = JsonSerializer.Create(
                new JsonSerializerSettings
                {
                    Converters =
                    {
                        materializer
                    }
                });

            var json = @"{ ""id"": ""1"", ""value"": 123, ""child"": { ""id"": ""1"" } }";
            var obj = serializer.Deserialize<LoopedObject>(new JsonTextReader(new StringReader(json)));

            Assert.NotNull(obj);
            Assert.Equal(123, obj.Value);
            Assert.NotNull(obj.Child);
            Assert.Same(obj, obj.Child);
        }

        private GqlMaterializer CreateMaterializer()
            => CreateMaterializer(string.Empty);

        private GqlMaterializer CreateMaterializer(string typeDefinitions, Action<SchemaBuilder> configure = null)
        {
            var schema = Schema.For(typeDefinitions, configure);
            schema.Initialize();
            var mapper = new Mock<IGraphTypeMapper>();
            var types = new Dictionary<Type, IGraphType>();
            mapper
                .Setup(_ => _.GetGraphType(It.IsAny<Type>()))
                .Returns((Type t) => schema.AllTypes.First(_ => _.Name == t.Name));

            return new GqlMaterializer(new EntityCache(mapper.Object));
        }

        private interface IFoo
        {
        }

        private abstract class Foo : IFoo
        {
        }

        private sealed class FooA : Foo
        {
            public int IntValue { get; set; }
        }

        private class FooB : Foo
        {
            public string StringValue { get; set; }
        }

        private class FooBB : FooB
        {
        }

        private sealed class Bar
        {
        }

        private sealed class LoopedObject
        {
            public int Value { get; set; }

            public LoopedObject Child { get; set; }
        }
    }
}

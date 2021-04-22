using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        [InlineData(typeof(uint), false)]
        [InlineData(typeof(long), false)]
        [InlineData(typeof(ulong), false)]
        [InlineData(typeof(float), false)]
        [InlineData(typeof(double), false)]
        [InlineData(typeof(decimal), false)]
        [InlineData(typeof(string), false)]
        [InlineData(typeof(object), true)]
        [InlineData(typeof(Array), true)]
        [InlineData(typeof(List<int>), true)]
        public void CanConvertTest(Type objectType, bool expectedCanConvert)
        {
            var materializer = new GqlMaterializer();
            var canConvert = materializer.CanConvert(objectType);
            Assert.Equal(expectedCanConvert, canConvert);
        }
    }
}

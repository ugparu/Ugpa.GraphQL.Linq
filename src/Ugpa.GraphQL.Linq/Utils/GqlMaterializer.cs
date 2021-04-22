using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Ugpa.GraphQL.Linq.Utils
{
    internal sealed class GqlMaterializer : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return Type.GetTypeCode(objectType) == TypeCode.Object;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var contract = serializer.ContractResolver.ResolveContract(objectType);

            if (existingValue is null)
            {
                return contract switch
                {
                    JsonArrayContract arrayContract => ReadArray(reader, arrayContract, serializer),
                    JsonObjectContract objectContract => ReadObject(reader, objectContract, serializer),
                    JsonPrimitiveContract => reader.Value,
                    _ => throw new NotImplementedException()
                };
            }
            else
            {
                switch (contract)
                {
                    case JsonPrimitiveContract:
                        {
                            return reader.Value;
                        }
                    default:
                        {
                            serializer.Populate(reader, existingValue);
                            return existingValue;
                        }
                }
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        private object ReadArray(JsonReader reader, JsonArrayContract arrayContract, JsonSerializer serializer)
        {
            if (arrayContract.OverrideCreator is not null)
            {
                throw new NotImplementedException();
            }
            else if (arrayContract.DefaultCreator is not null)
            {
                var value = arrayContract.DefaultCreator();
                serializer.Populate(reader, value);
                return value;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private object ReadObject(JsonReader reader, JsonObjectContract objectContract, JsonSerializer serializer)
        {
            var token = JToken.ReadFrom(reader);

            if (token.Children<JProperty>().FirstOrDefault(_ => _.Name == "__typename") is JProperty typeToken)
            {
                typeToken.Remove();
                var typeName = (string)((JValue)typeToken.Value).Value;

                var objectType = serializer.SerializationBinder.BindToType(null, typeName)
                    ?? throw new InvalidOperationException();

                reader = token.CreateReader();
                return ReadJson(reader, objectType, null, serializer);
            }
            else if (objectContract.OverrideCreator is not null)
            {
                throw new NotImplementedException();
            }
            else if (objectContract.DefaultCreator is not null)
            {
                var value = objectContract.DefaultCreator();
                serializer.Populate(token.CreateReader(), value);
                return value;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}

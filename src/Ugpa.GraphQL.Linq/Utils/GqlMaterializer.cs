using System;
using System.Collections;
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
            return
                Type.GetTypeCode(objectType) == TypeCode.Object &&
                !typeof(IEnumerable).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (existingValue is null)
            {
                var contract = (JsonObjectContract)serializer.ContractResolver.ResolveContract(objectType);
                return ReadObject(reader, contract, serializer);
            }
            else
            {
                PopulateObject(reader, existingValue, serializer);
                return existingValue;
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        private object ReadObject(JsonReader reader, JsonObjectContract objectContract, JsonSerializer serializer)
        {
            var token = (JObject)JToken.ReadFrom(reader);
            var objectType = objectContract.UnderlyingType;
            if (IsTypeExplicitlyDefined(token, serializer.SerializationBinder, ref objectType))
            {
                reader = token.CreateReader();
                return ReadJson(reader, objectType, null, serializer);
            }
            else
            {
                if (objectContract.UnderlyingType.IsAbstract)
                    throw new InvalidOperationException();

                var id = serializer.ReferenceResolver.GetReference(this, token);
                if (id is not null && serializer.ReferenceResolver.ResolveReference(this, id) is object value)
                {
                    serializer.Populate(token.CreateReader(), value);
                    return value;
                }

                if (objectContract.OverrideCreator is not null)
                {
                    throw new NotImplementedException();
                }
                else if (objectContract.DefaultCreator is not null)
                {
                    value = objectContract.DefaultCreator();
                    serializer.Populate(token.CreateReader(), value);
                }
                else
                {
                    throw new NotImplementedException();
                }

                if (id is not null)
                    serializer.ReferenceResolver.AddReference(this, id, value);

                return value;
            }
        }

        private void PopulateObject(JsonReader reader, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.ReadFrom(reader);

            var objectType = existingValue.GetType();
            var explicitType = objectType;

            if (IsTypeExplicitlyDefined(token, serializer.SerializationBinder, ref explicitType) && objectType != explicitType)
                throw new InvalidOperationException();

            serializer.Populate(token.CreateReader(), existingValue);
        }

        private bool IsTypeExplicitlyDefined(JToken token, ISerializationBinder binder, ref Type objectType)
        {
            if (token.Children<JProperty>().FirstOrDefault(_ => _.Name == "__typename") is JProperty typeToken)
            {
                typeToken.Remove();
                var typeName = (string)((JValue)typeToken.Value).Value;

                var explicitObjectType = binder.BindToType(null, typeName)
                    ?? throw new InvalidOperationException();

                if (!objectType.IsAssignableFrom(explicitObjectType))
                    throw new InvalidOperationException();

                objectType = explicitObjectType;
                return true;
            }

            return false;
        }
    }
}

using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Ugpa.GraphQL.Linq.Properties;

namespace Ugpa.GraphQL.Linq.Utils
{
    internal sealed class GqlMaterializer : JsonConverter
    {
        private readonly EntityCache entityCache;

        public GqlMaterializer(EntityCache entityCache)
        {
            this.entityCache = entityCache;
        }

        public override bool CanConvert(Type objectType)
        {
            return
                Type.GetTypeCode(objectType) == TypeCode.Object &&
                !typeof(IEnumerable).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
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

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
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

                if (entityCache.GetEntity(token, objectContract.UnderlyingType, out var id) is object value)
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
                    entityCache.PutEntity(id, value);

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
                var typeName = (string?)((JValue)typeToken.Value).Value
                    ?? throw new InvalidOperationException();

                var explicitObjectType = binder.BindToType(null, typeName)
                    ?? throw new InvalidOperationException(string.Format(Resources.GqlMaterializer_UnableBindToType, typeName));

                if (!objectType.IsAssignableFrom(explicitObjectType))
                    throw new InvalidOperationException(string.Format(Resources.GqlMaterializer_TypeIsNotAssignable, explicitObjectType, objectType));

                objectType = explicitObjectType;
                return true;
            }

            return false;
        }
    }
}

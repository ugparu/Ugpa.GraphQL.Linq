using System;
using System.Collections.Concurrent;
using System.Linq;
using GraphQL.Types;
using Newtonsoft.Json.Linq;

namespace Ugpa.GraphQL.Linq.Utils
{
    internal sealed class EntityCache
    {
        private readonly ConcurrentDictionary<IGraphType, ConcurrentDictionary<string, object>> cache = new();

        private readonly IGraphTypeMapper graphTypeMapper;

        public EntityCache(IGraphTypeMapper graphTypeMapper)
        {
            this.graphTypeMapper = graphTypeMapper ?? throw new ArgumentNullException(nameof(graphTypeMapper));
        }

        public void PutEntity(string id, object entity)
        {
            var gType = graphTypeMapper.GetGraphType(entity.GetType());
            var typeCache = cache.GetOrAdd(gType, _ => new ConcurrentDictionary<string, object>());
            typeCache.TryAdd(id, entity);
        }

        public object GetEntity(JObject tokent, Type objectType, out string id)
        {
            var gType = (ObjectGraphType)graphTypeMapper.GetGraphType(objectType) ?? throw new InvalidOperationException();

            var idFields = tokent.Properties()
                .Join(gType.Fields, _ => _.Name, _ => _.Name, (p, f) => new { p, f })
                .Where(_ => _.f.ResolvedType is IdGraphType)
                .ToArray();

            id = idFields.Length switch
            {
                0 => null,
                1 => (string)((JValue)idFields[0].p.Value).Value,
                _ => throw new NotImplementedException()
            };

            if (id is not null && cache.TryGetValue(gType, out var typeCache) && typeCache.TryGetValue(id, out var value))
            {
                return value;
            }

            return null;
        }
    }
}

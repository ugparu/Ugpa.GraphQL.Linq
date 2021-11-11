using System;
using System.Collections;
using System.Runtime.Serialization;
using Newtonsoft.Json.Serialization;

namespace Ugpa.GraphQL.Linq.Utils
{
    internal sealed class ListCleanupContractResolver : IContractResolver
    {
        private readonly IContractResolver innerResolver;

        public ListCleanupContractResolver(IContractResolver innerResolver)
        {
            this.innerResolver = innerResolver;
        }

        public JsonContract ResolveContract(Type type)
        {
            var contract = innerResolver.ResolveContract(type);

            if (contract is JsonArrayContract && !contract.OnDeserializingCallbacks.Contains(CleanupCollection))
                contract.OnDeserializingCallbacks.Add(CleanupCollection);

            return contract;
        }

        private void CleanupCollection(object o, StreamingContext context)
        {
            if (o is IList list && !list.IsReadOnly)
                list.Clear();
        }
    }
}

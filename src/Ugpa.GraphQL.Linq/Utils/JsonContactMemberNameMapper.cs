using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Serialization;

namespace Ugpa.GraphQL.Linq.Utils
{
    internal sealed class JsonContactMemberNameMapper : IMemberNameMapper
    {
        private readonly IContractResolver contractResolver;

        private readonly Dictionary<Type, JsonContract> contractCache = new();

        public JsonContactMemberNameMapper(IContractResolver contractResolver)
        {
            this.contractResolver = contractResolver;
        }

        public string GetFieldName(MemberInfo member)
        {
            if (!contractCache.TryGetValue(member.DeclaringType, out var contract))
            {
                contract = contractResolver.ResolveContract(member.DeclaringType);
                contractCache[member.DeclaringType] = contract;
            }

            if (contract is JsonObjectContract objContract)
            {
                return objContract.Properties.FirstOrDefault(_ => _.UnderlyingName == member.Name)?.PropertyName ?? member.Name;
            }
            else
            {
                return member.Name;
            }
        }
    }
}

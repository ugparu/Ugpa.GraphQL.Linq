using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;

namespace Ugpa.GraphQL.Linq.Utils
{
    internal sealed class VariablesResolver
    {
        private const string VariableNameFormat = "linq_param_{0}";

        private readonly Dictionary<object, Dictionary<string, (int Index, IGraphType Type, object Value)>> variables = new();

        private int varIndex = 0;

        public IEnumerable<(string Name, IGraphType Type, object Value)> GetAllVariables()
        {
            return variables.Values
                .SelectMany(_ => _.Values)
                .Select(_ => (string.Format(VariableNameFormat, _.Index), _.Type, _.Value));
        }

        public string GetArgumentVariableName(QueryArgument argument, object variablesSource)
        {
            if (!variables.TryGetValue(variablesSource, out var v1))
            {
                v1 = new Dictionary<string, (int, IGraphType, object)>();
                variables[variablesSource] = v1;
            }

            if (!v1.TryGetValue(argument.Name, out var v2))
            {
                var varValue = ResolveVariableValue(variablesSource, argument.Name);
                v2 = (varIndex++, argument.ResolvedType, varValue);
                v1[argument.Name] = v2;
            }

            return string.Format(VariableNameFormat, v2.Index);
        }

        private object ResolveVariableValue(object variablesSource, string argumentName)
        {
            if (variablesSource is IDictionary<string, object> dictionary)
            {
                throw new NotImplementedException();
            }
            else
            {
                var property = variablesSource.GetType().GetProperty(argumentName) ?? throw new InvalidOperationException();
                return property.GetValue(variablesSource);
            }
        }
    }
}

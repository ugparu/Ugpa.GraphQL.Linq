using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;

namespace Ugpa.GraphQL.Linq
{
    internal sealed class VariablesResolver
    {
        private const string VariableNameFormat = "linq_param_{0}";

        private readonly Dictionary<object, Dictionary<string, (int index, IGraphType type, object value)>> variables =
            new Dictionary<object, Dictionary<string, (int, IGraphType, object)>>();

        private int varIndex = 0;

        public IEnumerable<(string name, IGraphType type, object value)> GetAllVariables()
        {
            return variables.Values
                .SelectMany(_ => _.Values)
                .Select(_ => (string.Format(VariableNameFormat, _.index), _.type, _.value));
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

            return string.Format(VariableNameFormat, v2.index);
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

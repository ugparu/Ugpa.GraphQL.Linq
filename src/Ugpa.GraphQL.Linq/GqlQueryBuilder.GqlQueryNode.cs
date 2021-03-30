using System.Collections.Generic;
using System.Linq;
using System.Text;
using GraphQL.Types;

namespace Ugpa.GraphQL.Linq
{
    partial class GqlQueryBuilder
    {
        private class GqlQueryNode
        {
            private readonly FieldType field;
            private readonly VariablesResolver variablesResolver;
            private readonly object variableValuesSource;

            public GqlQueryNode(FieldType field, VariablesResolver variablesResolver, object variableValuesSource)
            {
                this.field = field;
                this.variablesResolver = variablesResolver;
                this.variableValuesSource = variableValuesSource;
            }

            public string Name => field.Name;

            public IGraphType GraphType => field.ResolvedType is IProvideResolvedType provideResolvedType ? provideResolvedType.ResolvedType : field.ResolvedType;

            public IEnumerable<QueryArgument> Arguments => field.Arguments;

            public ICollection<GqlQueryNode> Children { get; } = new List<GqlQueryNode>();

            public string ToQueryString()
            {
                var subBuilder = new StringBuilder();
                ToQueryString(subBuilder, string.Empty);

                var builder = new StringBuilder();
                builder.Append("query");

                var vars = variablesResolver.GetAllVariables().ToArray();
                if (vars.Any())
                {
                    var args = vars.Select(_ => $"${_.name}: {_.type}").ToArray();
                    var argsString = string.Join(", ", args);
                    builder.Append($"({argsString})");
                }

                builder.AppendLine(" {");
                builder.Append(subBuilder.ToString());
                builder.AppendLine("}");
                return builder.ToString();
            }

            public override string ToString()
                => ToQueryString();

            private void ToQueryString(StringBuilder queryBuilder, string indent)
            {
                indent += "  ";
                queryBuilder.Append($"{indent}{Name}");

                if (Arguments.Any())
                {
                    queryBuilder.Append("(");

                    var args = Arguments
                        .Select(argument =>
                        {
                            var varName = variablesResolver.GetArgumentVariableName(argument, variableValuesSource);
                            return $"{argument.Name}: ${varName}";
                        })
                        .ToArray();

                    queryBuilder.Append(string.Join(", ", args));

                    queryBuilder.Append(")");
                }

                queryBuilder.AppendLine(" {");

                foreach (var child in Children)
                {
                    if (child.Children.Any())
                        child.ToQueryString(queryBuilder, indent);
                    else
                        queryBuilder.AppendLine($"{indent}  {child.Name}");
                }

                queryBuilder.AppendLine($"{indent}}}");
            }
        }
    }
}

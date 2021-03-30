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
            private readonly string name;
            private readonly IEnumerable<QueryArgument> arguments;
            private readonly VariablesResolver variablesResolver;
            private readonly object variableValuesSource;

            public GqlQueryNode(string name, IGraphType graphType, IEnumerable<QueryArgument> arguments, VariablesResolver variablesResolver, object variableValuesSource)
            {
                this.name = name;
                this.arguments = arguments;
                this.variablesResolver = variablesResolver;
                this.variableValuesSource = variableValuesSource;

                GraphType = graphType;
            }

            public IGraphType GraphType { get; }

            public ICollection<GqlQueryNode> Children { get; } = new List<GqlQueryNode>();

            public ICollection<GqlQueryNode> PosibleTypes { get; } = new List<GqlQueryNode>();

            public static GqlQueryNode FromField(FieldType field, VariablesResolver variablesResolver, object variableValuesSource)
            {
                var graphType = field.ResolvedType is IProvideResolvedType provideResolvedType
                    ? provideResolvedType.ResolvedType
                    : field.ResolvedType;

                return new GqlQueryNode(field.Name, graphType, field.Arguments, variablesResolver, variableValuesSource);
            }

            public void Prune()
            {
                foreach (var group in Children.GroupBy(_ => _.name).Where(_ => _.Count() > 1))
                {
                    var main = group.First();
                    foreach (var other in group.Skip(1))
                    {
                        if (main.GraphType != other.GraphType)
                            throw new System.InvalidOperationException();

                        foreach (var child in other.Children)
                            main.Children.Add(child);

                        Children.Remove(other);
                    }
                }

                foreach (var child in Children)
                    child.Prune();
            }

            public string ToQueryString()
            {
                var subBuilder = new StringBuilder();
                ToQueryString(subBuilder, string.Empty, Enumerable.Empty<string>());

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
            {
                var builder = new StringBuilder(name);

                if (arguments.Any())
                    builder.Append($"({string.Join(", ", arguments.Select(_ => _.Name))})");

                if (Children.Any())
                {
                    builder.Append(" {");
                    builder.Append(string.Join(" ", Children.Select(_ => _.name)));
                    builder.Append(" }");
                }

                return builder.ToString();
            }

            private void ToQueryString(StringBuilder queryBuilder, string indent, IEnumerable<string> exclude)
            {
                indent += "  ";
                queryBuilder.Append($"{indent}{name}");

                if (arguments.Any())
                {
                    queryBuilder.Append("(");

                    var args = arguments
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

                if (GraphType is IAbstractGraphType)
                    queryBuilder.AppendLine($"{indent}  __typename");

                foreach (var child in Children.Where(_ => !exclude.Contains(_.name)))
                {
                    if (child.Children.Any())
                        child.ToQueryString(queryBuilder, indent, Enumerable.Empty<string>());
                    else
                        queryBuilder.AppendLine($"{indent}  {child.name}");
                }

                if (GraphType is IAbstractGraphType)
                {
                    foreach (var child in PosibleTypes)
                    {
                        queryBuilder.Append($"{indent}... on {child.GraphType.Name}");
                        child.ToQueryString(queryBuilder, indent, Children.Select(_ => _.name));
                    }
                }

                queryBuilder.AppendLine($"{indent}}}");
            }
        }
    }
}

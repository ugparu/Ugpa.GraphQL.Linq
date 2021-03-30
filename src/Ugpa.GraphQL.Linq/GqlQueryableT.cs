using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Ugpa.GraphQL.Linq
{
    internal abstract class GqlQueryable
    {
        protected GqlQueryable(string queryName)
        {
            QueryName = queryName;
        }

        public string QueryName { get; }
    }

    internal sealed class GqlQueryable<T> : GqlQueryable, IQueryable<T>
    {
        public GqlQueryable(GqlQueryProvider queryProvider, string queryName)
            : base(queryName)
        {
            Provider = queryProvider ?? throw new ArgumentNullException(nameof(queryProvider));
            Expression = Expression.Constant(this);
        }

        public GqlQueryable(GqlQueryProvider queryProvider, Expression expression)
            : base(null)
        {
            Provider = queryProvider ?? throw new ArgumentNullException(nameof(queryProvider));
            Expression = expression;
        }

        public Expression Expression { get; }

        public Type ElementType => typeof(T);

        public IQueryProvider Provider { get; }

        public IEnumerator<T> GetEnumerator()
            => Provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}

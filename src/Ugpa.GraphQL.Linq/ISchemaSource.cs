using GraphQL.Types;

namespace Ugpa.GraphQL.Linq
{
    public interface ISchemaSource
    {
        ISchema GetSchema();
    }
}

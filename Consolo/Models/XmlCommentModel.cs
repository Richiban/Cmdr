using System.Collections.Generic;
using static Consolo.Prelude;

namespace Consolo;

record XmlCommentModel
{
    public Option<string> Summary { get; init; }
    public IReadOnlyDictionary<string, string> Params { get; init; } =
        new Dictionary<string, string>();

    public Option<string> this[string parameterName] => Params.TryGetValue(parameterName, out var value)
        ? Some(value)
        : None;
}
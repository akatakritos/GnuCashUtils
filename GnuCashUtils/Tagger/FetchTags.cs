using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GnuCashUtils.Core;
using MediatR;

namespace GnuCashUtils.Tagger;

public record FetchTags() : IRequest<HashSet<Core.Tag>>;

public class FetchTagsHandler(IDbConnectionFactory db) : IRequestHandler<FetchTags, HashSet<Core.Tag>>
{
    public Task<HashSet<Core.Tag>> Handle(FetchTags request, CancellationToken cancellationToken)
    {
        using var connection = db.GetConnection();
        var set = new HashSet<Core.Tag>();
        using var reader =
            connection.ExecuteReader("select string_val from slots where name  = 'notes' and string_val like '%#[%'");
        while (reader.Read())
        {
            var value = reader["string_val"] as string;
            Debug.Assert(value is not null);
            var tags = Core.Tag.Parse(value);
            foreach (var tag in tags)
                set.Add(tag);
        }

        return Task.FromResult(set);
    }
}
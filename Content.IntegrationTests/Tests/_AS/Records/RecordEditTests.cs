using System.Linq;
using Content.Server.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Serialization.Manager;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._AS.Records;

[TestFixture]
public sealed class RecordEditTests
{
    private static ServerDbSqlite GetDb(RobustIntegrationTest.ServerIntegrationInstance server)
    {
        var cfg = server.ResolveDependency<IConfigurationManager>();
        var serialization = server.ResolveDependency<ISerializationManager>();
        var opsLog = server.ResolveDependency<ILogManager>().GetSawmill("db.ops");
        var builder = new DbContextOptionsBuilder<SqliteServerDbContext>();
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        builder.UseSqlite(conn);
        return new ServerDbSqlite(() => builder.Options, true, cfg, true, opsLog, serialization);
    }

    [Test]
    public async Task LastEditUpdateTest()
    {
        var pair = await PoolManager.GetServerClient();
        var db = GetDb(pair.Server);

        var created = await db.AddCharacterRecord(RecordType.PersonalNote, null, null, null, null);
        await db.HideRecord(created.Id, null, null, allowNonAuthor: true, updateEdits: true);
        var edits = await db.GetRecordEdits(created.Id);
        var edit = edits.FirstOrDefault();
        Assert.That(edit, Is.Not.Null, "Edit not found.");
        created = await db.GetCharacterRecord(created.Id);
        Assert.That(created?.LastEditId, Is.EqualTo(edit.RecordCharacterId), "LastEditId not updated.");

        await pair.CleanReturnAsync();
    }
}

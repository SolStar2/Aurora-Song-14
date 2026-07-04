using System.Collections.Generic;
using System.Linq;
using Content.Server.Database;
using Content.Shared._AS.PersistentSystems;
using Content.Shared.Preferences;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Configuration;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Serialization.Manager;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests._AS.Records.CharacterRecord;

[TestFixture]
public sealed class CharacterRecordHideTests
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
    public async Task HideCharacterRecordTest()
    {
        var pair = await PoolManager.GetServerClient();
        var db = GetDb(pair.Server);
        var record = await db.AddCharacterRecord(RecordType.PersonalNote, null, null, null, null);

        await db.HideRecord(record.Id, null, null, allowNonAuthor: true);
        var query = await db.GetFilteredCharacterRecords(RecordType.PersonalNote, hidden: null);
        Assert.That(query, Is.Not.Empty, "Hidden record not returned with hidden override.");
        Assert.That(query.Find(r => r.Id == record.Id).Hidden, Is.True, "Hidden flag not set.");
        query = await db.GetFilteredCharacterRecords(RecordType.PersonalNote);
        Assert.That(query, Is.Empty, "Hidden record returned.");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnhideCharacterRecordTest()
    {
        var pair = await PoolManager.GetServerClient();
        var db = GetDb(pair.Server);
        var record = await db.AddCharacterRecord(RecordType.PersonalNote, null, null, null, null);

        await db.HideRecord(record.Id, null, null, allowNonAuthor: true);
        await db.UnhideRecord(record.Id, null, null, allowNonAuthor: true);
        var query = await db.GetFilteredCharacterRecords(RecordType.PersonalNote);
        Assert.That(query, Is.Not.Empty, "Hidden record not unhidden.");
        Assert.That(query.Find(r => r.Id == record.Id).Hidden, Is.False, "Hidden flag not unset.");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HideCharacterRecordNullOwnershipTest()
    {
        var pair = await PoolManager.GetServerClient();
        var db = GetDb(pair.Server);
        var username = new NetUserId(new Guid("ef67ca59-1129-44ea-9fe2-bb3a094d866d"));
        var prefs = await db.InitPrefsAsync(username, new HumanoidCharacterProfile() { Name = "Non owner" });
        var profileId = prefs!.Profiles.Find(p => p.Slot == 0).Id;

        List<RecordCharacter> testRecords =
        [
            new(),
            new() { AuthorCharacterId = profileId },
        ];

        await Task.WhenAll(
            testRecords.Select(test =>
                db.AddCharacterRecord(
                    test.RecordType,
                    test.TargetCharacterId,
                    test.AuthorUserId,
                    test.AuthorCharacterId,
                    test.RoundId)));

        await Task.WhenAll(
            testRecords.Select(test =>
                db.HideRecord(test.Id, null, null)));

        foreach (var record in await db.GetFilteredCharacterRecords(RecordType.PersonalNote, hidden: null))
        {
            Assert.That(record.Hidden, Is.False, $"Record with {(record.AuthorUserId.HasValue ? "owner" : "no owner")} hidden by null owner.");
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HideCharacterRecordOwnershipTest()
    {
        var pair = await PoolManager.GetServerClient();
        var db = GetDb(pair.Server);
        var username = new NetUserId(new Guid("ef67ca59-1129-44ea-9fe2-bb3a094d866d"));
        await db.InitPrefsAsync(username, new HumanoidCharacterProfile());

        await db.SaveCharacterSlotAsync(username, new HumanoidCharacterProfile() { Name = "Owner"}, 0);
        await db.SaveCharacterSlotAsync(username, new HumanoidCharacterProfile() { Name = "Non Owner"}, 1);
        var prefs = await db.GetPlayerPreferencesAsync(username);
        var ownerId = prefs!.Profiles.Find(p => p.Slot == 0).Id;
        var otherId = prefs!.Profiles.Find(p => p.Slot == 1).Id;
        List<RecordCharacter> testRecords =
        [
            new(),
            new() { AuthorCharacterId = ownerId },
            new() { AuthorCharacterId = otherId },
        ];

        await Task.WhenAll(
            testRecords.Select(test =>
                db.AddCharacterRecord(
                    test.RecordType,
                    test.TargetCharacterId,
                    test.AuthorUserId,
                    test.AuthorCharacterId,
                    test.RoundId)));

        var records =  await db.GetFilteredCharacterRecords(RecordType.PersonalNote);

        await Task.WhenAll(
            records.Select(test =>
                db.HideRecord(test.Id, null, ownerId)));

        foreach (var record in await db.GetFilteredCharacterRecords(RecordType.PersonalNote, hidden: null))
        {
            if (record.AuthorCharacterId == ownerId)
                Assert.That(record.Hidden, Is.True, $"Owner failed to hide owned record.");
            else
                Assert.That(record.Hidden, Is.False, $"Record with {(record.AuthorUserId.HasValue ? "different owner" : "no owner")} hidden by non owner.");
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnhideCharacterRecordNullOwnershipTest()
    {
        var pair = await PoolManager.GetServerClient();
        var db = GetDb(pair.Server);
        var username = new NetUserId(new Guid("ef67ca59-1129-44ea-9fe2-bb3a094d866d"));
        var prefs = await db.InitPrefsAsync(username, new HumanoidCharacterProfile() { Name = "Non owner" });
        var profileId = prefs!.Profiles.Find(p => p.Slot == 0).Id;

        List<RecordCharacter> testRecords =
        [
            new(),
            new() { AuthorCharacterId = profileId },
        ];

        await Task.WhenAll(
            testRecords.Select(test =>
                db.AddCharacterRecord(
                    test.RecordType,
                    test.TargetCharacterId,
                    test.AuthorUserId,
                    test.AuthorCharacterId,
                    test.RoundId)));

        var records =  await db.GetFilteredCharacterRecords(RecordType.PersonalNote);

        await Task.WhenAll(
            records.Select(test =>
                db.HideRecord(test.Id, null, null, allowNonAuthor: true)));

        await Task.WhenAll(
            records.Select(test =>
                db.UnhideRecord(test.Id, null, null)));

        foreach (var record in await db.GetFilteredCharacterRecords(RecordType.PersonalNote, hidden: null))
        {
            Assert.That(record.Hidden, Is.True, $"Record with {(record.AuthorUserId.HasValue ? "owner" : "no owner")} unhidden by null owner.");
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnhideCharacterRecordOwnershipTest()
    {
        var pair = await PoolManager.GetServerClient();
        var db = GetDb(pair.Server);
        var username = new NetUserId(new Guid("ef67ca59-1129-44ea-9fe2-bb3a094d866d"));
        await db.InitPrefsAsync(username, new HumanoidCharacterProfile());

        await db.SaveCharacterSlotAsync(username, new HumanoidCharacterProfile() { Name = "Owner"}, 0);
        await db.SaveCharacterSlotAsync(username, new HumanoidCharacterProfile() { Name = "Non Owner"}, 1);
        var prefs = await db.GetPlayerPreferencesAsync(username);
        var ownerId = prefs!.Profiles.Find(p => p.Slot == 0).Id;
        var otherId = prefs!.Profiles.Find(p => p.Slot == 1).Id;
        List<RecordCharacter> testRecords =
        [
            new(),
            new() { AuthorCharacterId = ownerId },
            new() { AuthorCharacterId = otherId },
        ];

        await Task.WhenAll(
            testRecords.Select(test =>
                db.AddCharacterRecord(
                    test.RecordType,
                    test.TargetCharacterId,
                    test.AuthorUserId,
                    test.AuthorCharacterId,
                    test.RoundId)));

        var records = await db.GetFilteredCharacterRecords(RecordType.PersonalNote);

        await Task.WhenAll(
            records.Select(test =>
                db.HideRecord(test.Id, null, null, allowNonAuthor: true)));

        await Task.WhenAll(
            records.Select(test =>
                db.UnhideRecord(test.Id, null, ownerId)));

        foreach (var record in await db.GetFilteredCharacterRecords(RecordType.PersonalNote, hidden: null))
        {
            if (record.AuthorCharacterId == ownerId)
                Assert.That(record.Hidden, Is.False, $"Owner failed to unhide owned record.");
            else
                Assert.That(record.Hidden, Is.True, $"Record with {(record.AuthorUserId.HasValue ? "different owner" : "no owner")} unhidden by non owner.");
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HideResultCharacterRecordTest()
    {
        var pair = await PoolManager.GetServerClient();
        var db = GetDb(pair.Server);
        var record = await db.AddCharacterRecord(RecordType.PersonalNote, null, null, null, null);

        Assert.That(await db.HideRecord(record.Id, null, null),
            Is.EqualTo(RecordUpdateStatus.Prohibited),
            "Hiding a record as a non-owner without allowNonOwner override should be prohibited.");

        Assert.That(await db.HideRecord(-1, null, null, allowNonAuthor: true),
            Is.EqualTo(RecordUpdateStatus.NotFound),
            "Hiding a record id that does not exist should report NotFound.");

        Assert.That(await db.HideRecord(record.Id, null, null, allowNonAuthor: true),
            Is.EqualTo(RecordUpdateStatus.Updated),
            "Successfully hiding a visible record should report Updated.");

        Assert.That(await db.HideRecord(record.Id, null, null),
            Is.EqualTo(RecordUpdateStatus.Prohibited),
            "Hiding a record as a non-owner without allowNonOwner override should be Prohibited, even on an already hidden record.");

        Assert.That(await db.HideRecord(record.Id, null, null, allowNonAuthor: true),
            Is.EqualTo(RecordUpdateStatus.NoChange),
            "Hiding an already hidden record should report NoChange.");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnhideResultCharacterRecordTest()
    {
        var pair = await PoolManager.GetServerClient();
        var db = GetDb(pair.Server);
        var record = await db.AddCharacterRecord(RecordType.PersonalNote, null, null, null, null);

        Assert.That(await db.UnhideRecord(record.Id, null, null),
            Is.EqualTo(RecordUpdateStatus.Prohibited),
            "Unhiding a record as a non-owner without allowNonOwner override should be Prohibited, even on an already visible record.");

        Assert.That(await db.UnhideRecord(record.Id, null, null, allowNonAuthor: true),
            Is.EqualTo(RecordUpdateStatus.NoChange),
            "Unhiding an already visible record should report NoChange.");

        await db.HideRecord(record.Id, null, null, allowNonAuthor: true);

        Assert.That(await db.UnhideRecord(record.Id, null, null),
            Is.EqualTo(RecordUpdateStatus.Prohibited),
            "Unhiding a record as a non-owner without allowNonOwner override should be Prohibited.");

        Assert.That(await db.UnhideRecord(-1, null, null, allowNonAuthor: true),
            Is.EqualTo(RecordUpdateStatus.NotFound),
            "Unhiding a record id that does not exist should report NotFound.");

        Assert.That(await db.UnhideRecord(record.Id, null, null, allowNonAuthor: true),
            Is.EqualTo(RecordUpdateStatus.Updated),
            "Successfully Unhiding a hidden record should report Updated.");

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HideUpdateEditsTest()
    {
        var pair = await PoolManager.GetServerClient();
        var db = GetDb(pair.Server);

        var created = await db.AddCharacterRecord(RecordType.PersonalNote, null, null, null, null);
        await db.HideRecord(created.Id, null, null, allowNonAuthor: true);
        var edits = await db.GetRecordEdits(created.Id);
        Assert.That(edits, Is.Empty, "Edit added while updateEdits was not set.");

        created = await db.AddCharacterRecord(RecordType.PersonalNote, null, null, null, null);
        await db.HideRecord(created.Id, null, null, allowNonAuthor: true, updateEdits: true);
        edits = await db.GetRecordEdits(created.Id);
        Assert.That(edits, Is.Not.Empty, "Edit not added with updateEdits set.");

        await pair.CleanReturnAsync();
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

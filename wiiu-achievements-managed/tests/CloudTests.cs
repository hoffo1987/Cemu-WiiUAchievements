using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using WiiUAchievements;

static class CloudTests
{
    public static void Run(Action<bool, string> check, string output, bool portable)
    {
        var utc = DateTime.UtcNow.AddHours(-1);
        var store = new ProgressStore(Path.Combine(output, "cloud-merge"), " Alice ", "game", "revision");
        store.Award("old", utc);
        check(store.Merge(new[] { new Unlock { Id = "new", Utc = utc.AddMinutes(1) } }, new[] { "old", "new" }), "Cloud merge imports a missing ID atomically");
        check(store.Get("new").Utc == utc.AddMinutes(1) && store.Get("old").Utc == utc, "Cloud import preserves original UTC timestamps");
        check(!store.Merge(new[] { new Unlock { Id = "old", Utc = utc.AddMinutes(2) } }, new[] { "old" }) && store.Get("old").Utc == utc, "Cloud merge cannot overwrite earned timestamps");
        bool rejected = false;
        try { store.Merge(new[] { new Unlock { Id = "valid", Utc = utc }, new Unlock { Id = "planned", Utc = utc } }, new[] { "valid" }); } catch (InvalidDataException) { rejected = true; }
        check(rejected && !store.Contains("valid"), "Invalid remote batch commits no partial progress");
        var copy = store.Snapshot(); copy[0].Id = "mutated";
        check(store.Contains("old") && !store.Contains("mutated"), "Cloud worker receives defensive progress copies");
        check(CloudSync.ProfileKey(store.Identity) != CloudSync.ProfileKey("Bob|game|revision") && store.Identity == "Alice|game|revision", "Cloud identity preserves trimmed profile, game and revision isolation");
        var old = new GameSnapshot { MaxHealth = 20 }; var now = new GameSnapshot { MaxHealth = 12 };
        check(now.RegressesFrom(old) && now.PermanentRegressionCount(old) == 1, "Maximum-heart rollback resets continuity too");
        var regressionStore = new ProgressStore(Path.Combine(output, "heart-rollback"), "test", "synthetic");
        var engine = new AchievementEngine(new List<Achievement> { new Achievement { Id = "ten", Field = "rupees", Threshold = 10, Points = 1 } }, regressionStore);
        for (int i = 0; i < 6; i++) engine.Update(new GameSnapshot { MaxHealth = i < 3 ? 20 : 12, Health = 12, Rupees = i < 3 ? 0 : 10, Utc = utc.AddMilliseconds(100 * i) });
        check(!regressionStore.Contains("ten"), "Engine retains capacity in its defensive continuity copy and blocks crossing on rollback");
        if (portable) { Console.WriteLine("SKIP DPAPI cloud credential checks require Windows"); return; }
        var json = new JavaScriptSerializer(); int refreshes = 0, pushes = 0, gets = 0; bool offline = false;
        string sentBody = null, sentUrl = null;
        Func<string, string, string, string, string> transport = (method, url, body, token) =>
        {
            sentUrl = url;
            if (offline) throw new IOException("synthetic offline");
            if (url.Contains("identitytoolkit")) { sentBody = body; return "{ \"idToken\": \"TOKEN\", \"refreshToken\": \"REFRESH\", \"localId\": \"user1\", \"expiresIn\": \"1\" }"; }
            if (url.Contains("securetoken")) { refreshes++; return "{\"id_token\":\"NEW_TOKEN\",\"refresh_token\":\"NEW_REFRESH\",\"user_id\":\"user1\",\"expires_in\":\"3600\"}"; }
            if (method == "PATCH") { pushes++; sentBody = body; return "{}"; }
            gets++;
            return json.Serialize(new { documents = new[] { new { name = "projects/synthetic/databases/(default)/documents/users/user1/profiles/profile/unlocks/new", fields = new { utc = new { timestampValue = utc.ToString("o") } } } } });
        };
        string root = Path.Combine(output, "cloud-auth"); var cloud = new CloudSync(root, transport);
        cloud.Configure("SYNTHETIC_KEY", "synthetic-project"); cloud.SignIn("synthetic@example.invalid", "quote\"\n\t\\password");
        var request = json.Deserialize<Dictionary<string, object>>(sentBody);
        check((string)request["password"] == "quote\"\n\t\\password" && cloud.IsSignedIn, "Auth handles whitespace JSON and serializes password control characters correctly");
        check(!File.ReadAllText(Path.Combine(root, "cloud.xml")).Contains("REFRESH"), "Refresh credentials are DPAPI encrypted on disk");
        check(!File.Exists(Path.Combine(root, "cloud.xml.bak")), "Credential saves never retain backup tokens");
        var remote = cloud.Synchronize(store.Identity, new[] { new Unlock { Id = "old", Utc = utc } }, new[] { "old", "new" });
        check(refreshes == 1 && remote.Length == 1 && remote[0].Id == "new", "Expiring auth renews before pulling actual unlock documents");
        check(pushes == 1 && sentUrl.Contains("/unlocks/old?currentDocument.exists=false") && sentBody.Contains(utc.ToString("o")), "Upload creates actual ID and timestamp without overwriting other devices");
        cloud.Synchronize(store.Identity, new Unlock[0], new[] { "old" });
        check(refreshes == 1, "Unexpired tokens are reused");
        var restored = new CloudSync(root, transport);
        check(restored.IsSignedIn, "Protected refresh credential restores the session after application restart");
        restored.Synchronize(store.Identity, new Unlock[0], new[] { "new" });
        check(refreshes == 2, "Restarted session exchanges saved refresh token before Firestore access");
        offline = true; rejected = false;
        try { restored.Synchronize(store.Identity, store.Snapshot(), new[] { "old", "new" }); } catch (IOException) { rejected = true; }
        check(rejected && store.Contains("old") && store.Contains("new"), "Offline errors preserve durable local retry data");
        offline = false; restored.Synchronize(store.Identity, store.Snapshot(), new[] { "old", "new" });
        check(pushes == 2, "Retry includes older locally saved unlocks, not only the latest award");
        File.WriteAllText(Path.Combine(root, "cloud.xml.bak"), "SYNTHETIC_LEGACY_TOKEN"); restored.SignOut();
        check(!restored.IsSignedIn && !File.Exists(Path.Combine(root, "cloud.xml.bak")) && !File.ReadAllText(Path.Combine(root, "cloud.xml")).Contains("NEW_REFRESH"), "Logout removes active and legacy backup credentials");
        cloud.Configure("OTHER_KEY", "other-project");
        check(!cloud.IsSignedIn, "Changing Firebase project clears the previous project session");
        string legacyRoot = Path.Combine(output, "cloud-legacy"); Directory.CreateDirectory(legacyRoot);
        new XDocument(new XElement("cloud", new XElement("apiKey", "KEY"), new XElement("projectId", "synthetic-project"), new XElement("userId", "user1"), new XElement("refreshToken", "LEGACY_REFRESH"))).Save(Path.Combine(legacyRoot, "cloud.xml"));
        File.Copy(Path.Combine(legacyRoot, "cloud.xml"), Path.Combine(legacyRoot, "cloud.xml.bak"));
        var migrated = new CloudSync(legacyRoot, transport);
        check(migrated.IsSignedIn && !File.ReadAllText(Path.Combine(legacyRoot, "cloud.xml")).Contains("LEGACY_REFRESH") && !File.Exists(Path.Combine(legacyRoot, "cloud.xml.bak")), "Legacy plaintext credential migrates to encrypted storage without backup");
        int pageRequests = 0;
        var paginated = new CloudSync(Path.Combine(output, "cloud-pages"), (method, url, body, token) =>
        {
            if (url.Contains("identitytoolkit")) return "{\"idToken\":\"TOKEN\",\"refreshToken\":\"REFRESH\",\"localId\":\"user1\",\"expiresIn\":\"3600\"}";
            pageRequests++;
            return pageRequests == 1 ? "{\"nextPageToken\":\"page 2\"}" : json.Serialize(new { documents = new[] { new { name = "documents/new", fields = new { utc = new { timestampValue = utc.ToString("o") } } } } });
        });
        paginated.Configure("KEY", "synthetic-project"); paginated.SignIn("test@example.invalid", "synthetic");
        check(paginated.Synchronize("identity", new Unlock[0], new[] { "new" }).Length == 1 && pageRequests == 2, "Cloud pull follows all paginated results");
        pageRequests = 0;
        check(paginated.Synchronize("identity", new Unlock[0], new[] { "other" }).Length == 0, "Cloud pull excludes unknown and unimplemented IDs");
        var reload = new ProgressStore(Path.Combine(output, "cloud-merge"), "Alice", "game", "revision");
        check(reload.Contains("new") && reload.Get("new").Utc == utc.AddMinutes(1), "Cloud-imported unlock survives disk reload with its timestamp");
    }
}

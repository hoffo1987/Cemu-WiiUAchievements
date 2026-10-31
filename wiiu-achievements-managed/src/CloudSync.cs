using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace WiiUAchievements
{
    // Call from the serialized background worker, never from the sampling thread.
    public sealed class CloudSync
    {
        public string UserId { get; private set; }
        public string Email { get; private set; }
        public string ApiKey { get; private set; }
        public string ProjectId { get; private set; }
        public string Warning { get; private set; }
        public bool IsSignedIn { get { return !String.IsNullOrEmpty(refreshToken); } }
        string idToken, refreshToken;
        DateTime expires;
        readonly string configPath;
        readonly Func<string, string, string, string, string> transport;
        readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024, RecursionLimit = 32 };
        public CloudSync(string dataRoot) : this(dataRoot, null) { }
        internal CloudSync(string dataRoot, Func<string, string, string, string, string> request)
        {
            configPath = Path.Combine(dataRoot, "cloud.xml"); transport = request ?? Request;
            if (!File.Exists(configPath)) return;
            try
            {
                var root = SafeXml.Load(configPath).Root;
                ApiKey = (string)root.Element("apiKey"); ProjectId = (string)root.Element("projectId");
                Email = (string)root.Element("email"); UserId = (string)root.Element("userId");
                string encrypted = (string)root.Element("protectedRefreshToken");
                if (!String.IsNullOrEmpty(encrypted)) refreshToken = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser));
                // Migrate the old unprotected credential; immediately remove old backup copies.
                else refreshToken = (string)root.Element("refreshToken");
                if (!String.IsNullOrEmpty(refreshToken) && String.IsNullOrEmpty(UserId)) { refreshToken = null; Warning = "Ripeti l'accesso cloud per aggiornare le credenziali."; }
                SaveConfig();
            }
            catch { refreshToken = idToken = null; Warning = "Configurazione cloud non leggibile: ripeti configurazione e accesso."; }
        }
        public void Configure(string apiKey, string projectId)
        {
            if (String.IsNullOrWhiteSpace(apiKey) || apiKey.Length > 256 || !Regex.IsMatch(apiKey, "^[A-Za-z0-9_-]+$")) throw new ArgumentException("API key Firebase non valida.");
            if (String.IsNullOrWhiteSpace(projectId) || !Regex.IsMatch(projectId, "^[a-z][a-z0-9-]{4,61}[a-z0-9]$")) throw new ArgumentException("Project ID Firebase non valido.");
            if (ApiKey != apiKey || ProjectId != projectId) { refreshToken = idToken = UserId = null; }
            ApiKey = apiKey; ProjectId = projectId; SaveConfig();
        }
        void SaveConfig()
        {
            string encrypted = String.IsNullOrEmpty(refreshToken) ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(refreshToken), null, DataProtectionScope.CurrentUser));
            // Never make a plaintext backup of a credential, including during migration/logout.
            string temporary = configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(configPath)));
            try
            {
                new XDocument(new XElement("cloud", new XElement("apiKey", ApiKey ?? ""), new XElement("projectId", ProjectId ?? ""),
                    new XElement("email", Email ?? ""), new XElement("userId", UserId ?? ""), new XElement("protectedRefreshToken", encrypted))).Save(temporary);
                if (File.Exists(configPath)) File.Replace(temporary, configPath, null); else File.Move(temporary, configPath);
                if (File.Exists(configPath + ".bak")) File.Delete(configPath + ".bak");
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public void SignIn(string email, string password) { Authenticate("signInWithPassword", email, password); }
        public void SignUp(string email, string password) { Authenticate("signUp", email, password); }
        void Authenticate(string operation, string email, string password)
        {
            if (String.IsNullOrEmpty(ApiKey) || String.IsNullOrEmpty(ProjectId)) throw new InvalidOperationException("Configura prima API key e Project ID.");
            idToken = refreshToken = UserId = null; SaveConfig();
            var body = json.Serialize(new { email = email, password = password, returnSecureToken = true });
            var response = Parse(transport("POST", "https://identitytoolkit.googleapis.com/v1/accounts:" + operation + "?key=" + Uri.EscapeDataString(ApiKey), body, null));
            SetTokens(response, "idToken", "refreshToken", "localId", "expiresIn"); Email = email; SaveConfig();
        }
        void SetTokens(Dictionary<string, object> response, string tokenKey, string refreshKey, string userKey, string expiryKey)
        {
            string token = Text(response, tokenKey), refresh = Text(response, refreshKey), user = Text(response, userKey);
            int seconds;
            if (String.IsNullOrEmpty(token) || String.IsNullOrEmpty(refresh) || !Regex.IsMatch(user ?? "", "^[A-Za-z0-9_-]{1,128}$") ||
                !Int32.TryParse(Text(response, expiryKey), out seconds) || seconds <= 0 || seconds > 86400) throw new InvalidDataException("Risposta autenticazione non valida.");
            idToken = token; refreshToken = refresh; UserId = user; expires = DateTime.UtcNow.AddSeconds(seconds);
        }
        void EnsureToken()
        {
            if (!IsSignedIn) throw new InvalidOperationException("Accedi prima all'account cloud.");
            if (!String.IsNullOrEmpty(idToken) && DateTime.UtcNow < expires.AddSeconds(-60)) return;
            var response = Parse(transport("FORM", "https://securetoken.googleapis.com/v1/token?key=" + Uri.EscapeDataString(ApiKey),
                "grant_type=refresh_token&refresh_token=" + Uri.EscapeDataString(refreshToken), null));
            string previousUser = UserId;
            if (previousUser != null && Text(response, "user_id") != previousUser) throw new InvalidDataException("Identità cloud cambiata durante il rinnovo.");
            SetTokens(response, "id_token", "refresh_token", "user_id", "expires_in"); SaveConfig();
        }
        public void SignOut()
        {
            idToken = refreshToken = UserId = null; expires = DateTime.MinValue;
            // Remove all old credential-bearing copies even if the next write fails.
            foreach (string path in new[] { configPath, configPath + ".bak" }) if (File.Exists(path)) File.Delete(path);
            SaveConfig();
        }
        public static string ProfileKey(string identity)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant();
        }
        // Local progress is a durable retry queue. Only absent remote IDs are created;
        // existing remote records are immutable and other devices cannot be overwritten.
        public Unlock[] Synchronize(string identity, Unlock[] local, IEnumerable<string> allowedIds)
        {
            EnsureToken();
            var allowed = new HashSet<string>(allowedIds, StringComparer.Ordinal);
            string collection = "https://firestore.googleapis.com/v1/projects/" + ProjectId + "/databases/(default)/documents/users/" + UserId + "/profiles/" + ProfileKey(identity) + "/unlocks";
            var remote = new Dictionary<string, Unlock>(StringComparer.Ordinal); string page = null;
            var pages = new HashSet<string>();
            do
            {
                if (!pages.Add(page ?? "")) throw new InvalidDataException("Paginazione cloud non valida.");
                var result = Parse(transport("GET", collection + "?pageSize=100" + (page == null ? "" : "&pageToken=" + Uri.EscapeDataString(page)), null, idToken));
                object documents;
                if (result.TryGetValue("documents", out documents)) foreach (object item in (IEnumerable)documents)
                {
                    var document = (Dictionary<string, object>)item;
                    string name = Text(document, "name"), id = name.Substring(name.LastIndexOf('/') + 1);
                    if (!allowed.Contains(id)) continue;
                    var fields = (Dictionary<string, object>)document["fields"];
                    DateTime utc;
                    string timestamp = Text((Dictionary<string, object>)fields["utc"], "timestampValue");
                    if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out utc) || utc.Kind != DateTimeKind.Utc || utc == DateTime.MinValue || utc > DateTime.UtcNow.AddMinutes(5))
                        throw new InvalidDataException("Timestamp cloud non valido.");
                    remote[id] = new Unlock { Id = id, Utc = utc };
                }
                page = Text(result, "nextPageToken"); if (String.IsNullOrEmpty(page)) page = null;
                if (pages.Count > 100) throw new InvalidDataException("Troppi risultati cloud.");
            } while (page != null);
            foreach (var unlock in local.Where(u => allowed.Contains(u.Id) && !remote.ContainsKey(u.Id)))
            {
                EnsureToken();
                string body = json.Serialize(new { fields = new { utc = new { timestampValue = unlock.Utc.ToString("o", CultureInfo.InvariantCulture) } } });
                try { transport("PATCH", collection + "/" + Uri.EscapeDataString(unlock.Id) + "?currentDocument.exists=false", body, idToken); }
                catch (WebException ex)
                {
                    var response = ex.Response as HttpWebResponse;
                    if (response == null || (response.StatusCode != HttpStatusCode.Conflict && response.StatusCode != HttpStatusCode.PreconditionFailed)) throw;
                    response.Close();
                }
            }
            return remote.Values.ToArray();
        }
        Dictionary<string, object> Parse(string value) { return json.Deserialize<Dictionary<string, object>>(value); }
        static string Text(Dictionary<string, object> map, string key) { object value; return map.TryGetValue(key, out value) ? value as string : null; }
        static string Request(string method, string url, string body, string token)
        {
            var request = (HttpWebRequest)WebRequest.Create(url); request.Method = method == "FORM" ? "POST" : method;
            request.ContentType = method == "FORM" ? "application/x-www-form-urlencoded" : "application/json";
            request.Timeout = 8000; request.ReadWriteTimeout = 8000; request.AllowAutoRedirect = false;
            if (!String.IsNullOrEmpty(token)) request.Headers["Authorization"] = "Bearer " + token;
            if (body != null) { byte[] bytes = Encoding.UTF8.GetBytes(body); request.ContentLength = bytes.Length; using (var stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length); }
            using (var response = request.GetResponse()) using (var stream = response.GetResponseStream()) using (var reader = new StreamReader(stream))
            {
                var output = new StringBuilder(); var buffer = new char[4096]; int count;
                while ((count = reader.Read(buffer, 0, buffer.Length)) > 0) { if (output.Length + count > 1024 * 1024) throw new InvalidDataException("Risposta cloud troppo grande."); output.Append(buffer, 0, count); }
                return output.ToString();
            }
        }
    }
}

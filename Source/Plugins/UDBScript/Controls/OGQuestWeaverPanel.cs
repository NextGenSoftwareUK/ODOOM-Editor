using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using OGEditorSDK;

namespace CodeImp.DoomBuilder.UDBScript
{
    /// <summary>
    /// Docker panel that lets map authors bind OASIS quest objectives to map triggers
    /// via the OASIS STAR API.  Requires the STAR API to be running; shows an offline
    /// message when it is not reachable.
    /// </summary>
    public class OGQuestWeaverPanel : UserControl
    {
        // ── Config ────────────────────────────────────────────────────────────────

        private static readonly string CONFIG_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UltimateDoomBuilder",
            "ogengine_config.json");

        private const string DEFAULT_API_URL = "http://localhost:5000";

        private static readonly string[] ALL_GAMES =
        {
            "ODOOM", "OQUAKE", "OQUAKE2", "OQUAKE2-RTX", "OQUAKE3",
            "ODUKE3D", "ODUKE3D-RT", "OWOLF3D", "ODOOM3", "ODOOM3-BFG"
        };

        // ── Controls ──────────────────────────────────────────────────────────────

        private Label    lblGame;
        private ComboBox comboGame;
        private Label    lblQuest;
        private ComboBox comboQuest;
        private Label    lblObjective;
        private ComboBox comboObjective;
        private Label    lblTriggerType;
        private ComboBox comboTriggerType;
        private Label    lblTriggerValue;
        private TextBox  txtTriggerValue;
        private Button   btnBind;
        private Label    lblStatus;
        private Panel    pnlSeparator;
        private Label    lblConfigHeader;
        private Label    lblApiUrl;
        private TextBox  txtApiUrl;
        private Label    lblAvatarId;
        private TextBox  txtAvatarId;
        private Button   btnSaveConfig;

        // ── State ─────────────────────────────────────────────────────────────────

        private string _apiUrl   = DEFAULT_API_URL;
        private string _avatarId = string.Empty;

        private System.Collections.Generic.List<QuestItem> _quests     = new System.Collections.Generic.List<QuestItem>();
        private System.Collections.Generic.List<QuestItem> _objectives = new System.Collections.Generic.List<QuestItem>();

        // ── Construction ──────────────────────────────────────────────────────────

        public OGQuestWeaverPanel()
        {
            InitializeComponent();
            LoadConfig();
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            BackColor   = SystemColors.Control;
            Padding     = new Padding(6);
            MinimumSize = new Size(200, 420);
            Size        = new Size(260, 490);
            AutoScroll  = true;

            const int x = 6;
            const int w = 240;
            int y = 8;

            // Game
            lblGame = new Label { Text = "Game:", AutoSize = true, Location = new Point(x, y) };
            y += 18;
            comboGame = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location      = new Point(x, y),
                Width         = w,
            };
            foreach (string g in ALL_GAMES) comboGame.Items.Add(g);
            comboGame.SelectedIndex        = 0;
            comboGame.SelectedIndexChanged += ComboGame_SelectedIndexChanged;
            y += 30;

            // Quest
            lblQuest = new Label { Text = "Quest:", AutoSize = true, Location = new Point(x, y) };
            y += 18;
            comboQuest = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location      = new Point(x, y),
                Width         = w,
            };
            comboQuest.Items.Add("(select game first)");
            comboQuest.SelectedIndex        = 0;
            comboQuest.SelectedIndexChanged += ComboQuest_SelectedIndexChanged;
            y += 30;

            // Objective
            lblObjective = new Label { Text = "Objective:", AutoSize = true, Location = new Point(x, y) };
            y += 18;
            comboObjective = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location      = new Point(x, y),
                Width         = w,
            };
            comboObjective.Items.Add("(select quest first)");
            comboObjective.SelectedIndex = 0;
            y += 30;

            // Trigger Type
            lblTriggerType = new Label { Text = "Trigger Type:", AutoSize = true, Location = new Point(x, y) };
            y += 18;
            comboTriggerType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location      = new Point(x, y),
                Width         = w,
            };
            comboTriggerType.Items.Add("Sector Tag");
            comboTriggerType.Items.Add("Thing Tag");
            comboTriggerType.Items.Add("LineDef Tag");
            comboTriggerType.Items.Add("Script Number");
            comboTriggerType.SelectedIndex = 0;
            y += 30;

            // Trigger Value
            lblTriggerValue = new Label { Text = "Trigger Value:", AutoSize = true, Location = new Point(x, y) };
            y += 18;
            txtTriggerValue = new TextBox { Location = new Point(x, y), Width = w, Text = "0" };
            y += 30;

            // Bind button
            btnBind = new Button
            {
                Text      = "⚔ Bind Objective to Map Trigger",
                Location  = new Point(x, y),
                Width     = w,
                Height    = 28,
                FlatStyle = FlatStyle.Standard,
            };
            btnBind.Click += BtnBind_Click;
            y += 36;

            // Status label
            lblStatus = new Label
            {
                Text        = "Ready",
                AutoSize    = true,
                Location    = new Point(x, y),
                MaximumSize = new Size(w, 0),
                ForeColor   = SystemColors.GrayText,
                Font        = new Font(Font.FontFamily, Font.Size - 1f),
            };
            y += 32;

            // Separator
            pnlSeparator = new Panel
            {
                Location  = new Point(x, y),
                Width     = w,
                Height    = 1,
                BackColor = SystemColors.ControlDark,
            };
            y += 9;

            // Config section header
            lblConfigHeader = new Label
            {
                Text     = "STAR API Config:",
                AutoSize = true,
                Location = new Point(x, y),
                Font     = new Font(Font.FontFamily, Font.Size, FontStyle.Bold),
            };
            y += 20;

            // API URL
            lblApiUrl = new Label { Text = "API URL:", AutoSize = true, Location = new Point(x, y) };
            y += 18;
            txtApiUrl = new TextBox { Location = new Point(x, y), Width = w, Text = DEFAULT_API_URL };
            y += 30;

            // Avatar ID
            lblAvatarId = new Label { Text = "Avatar ID:", AutoSize = true, Location = new Point(x, y) };
            y += 18;
            txtAvatarId = new TextBox { Location = new Point(x, y), Width = w, Text = string.Empty };
            y += 30;

            // Save Config button
            btnSaveConfig = new Button
            {
                Text      = "Save Config",
                Location  = new Point(x, y),
                Width     = w,
                Height    = 24,
                FlatStyle = FlatStyle.Standard,
            };
            btnSaveConfig.Click += BtnSaveConfig_Click;

            Controls.Add(lblGame);
            Controls.Add(comboGame);
            Controls.Add(lblQuest);
            Controls.Add(comboQuest);
            Controls.Add(lblObjective);
            Controls.Add(comboObjective);
            Controls.Add(lblTriggerType);
            Controls.Add(comboTriggerType);
            Controls.Add(lblTriggerValue);
            Controls.Add(txtTriggerValue);
            Controls.Add(btnBind);
            Controls.Add(lblStatus);
            Controls.Add(pnlSeparator);
            Controls.Add(lblConfigHeader);
            Controls.Add(lblApiUrl);
            Controls.Add(txtApiUrl);
            Controls.Add(lblAvatarId);
            Controls.Add(txtAvatarId);
            Controls.Add(btnSaveConfig);

            ResumeLayout(false);
        }

        // ── Config ────────────────────────────────────────────────────────────────

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(CONFIG_PATH))
                {
                    string json = File.ReadAllText(CONFIG_PATH);
                    Match mUrl  = Regex.Match(json, @"""star_api_url""\s*:\s*""([^""]+)""");
                    Match mAvid = Regex.Match(json, @"""avatar_id""\s*:\s*""([^""]*)""");
                    if (mUrl.Success)  _apiUrl   = mUrl.Groups[1].Value;
                    if (mAvid.Success) _avatarId = mAvid.Groups[1].Value;
                }
            }
            catch { /* use defaults on any IO/parse error */ }

            txtApiUrl.Text   = _apiUrl;
            txtAvatarId.Text = _avatarId;
        }

        private void SaveConfig()
        {
            _apiUrl   = txtApiUrl.Text.Trim();
            _avatarId = txtAvatarId.Text.Trim();

            try
            {
                string dir = Path.GetDirectoryName(CONFIG_PATH);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = "{ \"star_api_url\": \"" + EscapeJson(_apiUrl)
                            + "\", \"avatar_id\": \"" + EscapeJson(_avatarId) + "\" }";
                File.WriteAllText(CONFIG_PATH, json, System.Text.Encoding.UTF8);
                SetStatus("Config saved.", false);
            }
            catch (Exception ex)
            {
                SetStatus("Save failed: " + ex.Message, true);
            }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        // ── Quest loading ─────────────────────────────────────────────────────────

        private void LoadQuests(string gameId)
        {
            SetStatus("Loading quests...", false);
            comboQuest.Items.Clear();
            comboQuest.Items.Add("Loading...");
            comboQuest.SelectedIndex = 0;
            comboObjective.Items.Clear();
            comboObjective.Items.Add("(select quest first)");
            comboObjective.SelectedIndex = 0;
            _quests.Clear();
            _objectives.Clear();

            string apiUrl   = _apiUrl;
            string avatarId = _avatarId;
            string resultJson  = null;
            string resultError = null;

            Task.Run(() =>
            {
                try
                {
                    using (var client = new OGStarApiClient(apiUrl, avatarId))
                        resultJson = client.GetQuests(gameId);
                }
                catch (Exception ex)
                {
                    resultError = IsConnectionError(ex)
                        ? "Offline — enter API URL in config"
                        : "Error: " + ex.Message;
                }
            }).ContinueWith(t =>
            {
                if (IsHandleCreated)
                    BeginInvoke(new Action(() => OnQuestsLoaded(resultJson, resultError)));
            });
        }

        private void OnQuestsLoaded(string json, string errorMsg)
        {
            comboQuest.Items.Clear();
            _quests.Clear();

            if (errorMsg != null)
            {
                SetStatus(errorMsg, true);
                comboQuest.Items.Add("(offline)");
                comboQuest.SelectedIndex = 0;
                return;
            }

            if (!string.IsNullOrEmpty(json))
                _quests = ParseItems(json);

            if (_quests.Count == 0)
            {
                comboQuest.Items.Add("(no quests found)");
                comboQuest.SelectedIndex = 0;
                SetStatus("No quests for this game.", false);
            }
            else
            {
                foreach (var q in _quests) comboQuest.Items.Add(q.Title);
                comboQuest.SelectedIndex = 0;
                SetStatus("Ready", false);
            }
        }

        // ── Objective loading ─────────────────────────────────────────────────────

        private void LoadObjectives(string questId)
        {
            SetStatus("Loading objectives...", false);
            comboObjective.Items.Clear();
            comboObjective.Items.Add("Loading...");
            comboObjective.SelectedIndex = 0;
            _objectives.Clear();

            string apiUrl   = _apiUrl;
            string avatarId = _avatarId;
            string resultJson  = null;
            string resultError = null;

            Task.Run(() =>
            {
                try
                {
                    using (var client = new OGStarApiClient(apiUrl, avatarId))
                        resultJson = client.GetMissions(questId);
                }
                catch (Exception ex)
                {
                    resultError = IsConnectionError(ex)
                        ? "Offline — enter API URL in config"
                        : "Error: " + ex.Message;
                }
            }).ContinueWith(t =>
            {
                if (IsHandleCreated)
                    BeginInvoke(new Action(() => OnObjectivesLoaded(resultJson, resultError)));
            });
        }

        private void OnObjectivesLoaded(string json, string errorMsg)
        {
            comboObjective.Items.Clear();
            _objectives.Clear();

            if (errorMsg != null)
            {
                SetStatus(errorMsg, true);
                comboObjective.Items.Add("(offline)");
                comboObjective.SelectedIndex = 0;
                return;
            }

            if (!string.IsNullOrEmpty(json))
                _objectives = ParseItems(json);

            if (_objectives.Count == 0)
            {
                comboObjective.Items.Add("(no objectives found)");
                comboObjective.SelectedIndex = 0;
                SetStatus("No objectives for this quest.", false);
            }
            else
            {
                foreach (var o in _objectives) comboObjective.Items.Add(o.Title);
                comboObjective.SelectedIndex = 0;
                SetStatus("Ready", false);
            }
        }

        // ── Bind ──────────────────────────────────────────────────────────────────

        private void DoBind()
        {
            int questIdx = comboQuest.SelectedIndex;
            if (questIdx < 0 || questIdx >= _quests.Count)
            {
                SetStatus("Select a valid quest.", true);
                return;
            }

            int objIdx = comboObjective.SelectedIndex;
            if (objIdx < 0 || objIdx >= _objectives.Count)
            {
                SetStatus("Select a valid objective.", true);
                return;
            }

            int triggerValue;
            if (!int.TryParse(txtTriggerValue.Text.Trim(), out triggerValue))
            {
                SetStatus("Trigger value must be an integer.", true);
                return;
            }

            string questId     = _quests[questIdx].Id;
            string objectiveId = _objectives[objIdx].Id;
            string gameId      = comboGame.SelectedItem?.ToString() ?? "ODOOM";
            string triggerType = TriggerTypeToApiString(comboTriggerType.SelectedItem?.ToString() ?? "Sector Tag");
            string apiUrl      = _apiUrl;
            string avatarId    = _avatarId;

            string body = "{\"objectiveId\":\"" + EscapeJson(objectiveId) + "\","
                        + "\"triggerType\":\"" + EscapeJson(triggerType) + "\","
                        + "\"triggerValue\":" + triggerValue + ","
                        + "\"gameId\":\"" + EscapeJson(gameId) + "\"}";

            SetStatus("Binding...", false);
            btnBind.Enabled = false;

            string resultJson  = null;
            string resultError = null;

            Task.Run(() =>
            {
                try
                {
                    using (var client = new OGStarApiClient(apiUrl, avatarId))
                        resultJson = client.BindObjective(questId, body);
                }
                catch (Exception ex)
                {
                    resultError = IsConnectionError(ex)
                        ? "Offline — enter API URL in config"
                        : "Bind failed: " + ex.Message;
                }
            }).ContinueWith(t =>
            {
                if (IsHandleCreated)
                    BeginInvoke(new Action(() =>
                    {
                        btnBind.Enabled = true;
                        if (resultError != null)
                        {
                            SetStatus(resultError, true);
                        }
                        else
                        {
                            string preview = resultJson ?? string.Empty;
                            if (preview.Length > 60) preview = preview.Substring(0, 60) + "...";
                            SetStatus("Bound. " + preview, false);
                        }
                    }));
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string TriggerTypeToApiString(string uiValue)
        {
            switch (uiValue)
            {
                case "Sector Tag":    return "SectorTag";
                case "Thing Tag":     return "ThingTag";
                case "LineDef Tag":   return "LineDefTag";
                case "Script Number": return "ScriptNumber";
                default:              return uiValue;
            }
        }

        private static bool IsConnectionError(Exception ex)
        {
            if (ex is System.Net.WebException)
                return true;
            string msg = ex.Message ?? string.Empty;
            return msg.IndexOf("refused", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("connect", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Parses a JSON array of objects that each carry an "id" and "title" field.
        /// Uses Regex only — no JSON library required.
        /// </summary>
        private static System.Collections.Generic.List<QuestItem> ParseItems(string json)
        {
            var result     = new System.Collections.Generic.List<QuestItem>();
            var idRegex    = new Regex(@"""id""\s*:\s*""([^""]+)""");
            var titleRegex = new Regex(@"""title""\s*:\s*""([^""]+)""");

            MatchCollection ids    = idRegex.Matches(json);
            MatchCollection titles = titleRegex.Matches(json);

            for (int i = 0; i < ids.Count; i++)
            {
                string id    = ids[i].Groups[1].Value;
                string title = (i < titles.Count) ? titles[i].Groups[1].Value : id;
                result.Add(new QuestItem(id, title));
            }
            return result;
        }

        private void SetStatus(string message, bool isError)
        {
            lblStatus.Text      = message;
            lblStatus.ForeColor = isError ? Color.Red : SystemColors.GrayText;
        }

        // ── Event handlers ────────────────────────────────────────────────────────

        private void ComboGame_SelectedIndexChanged(object sender, EventArgs e)
        {
            string gameId = comboGame.SelectedItem?.ToString() ?? "ODOOM";
            LoadQuests(gameId);
        }

        private void ComboQuest_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = comboQuest.SelectedIndex;
            if (idx >= 0 && idx < _quests.Count)
                LoadObjectives(_quests[idx].Id);
        }

        private void BtnBind_Click(object sender, EventArgs e)   => DoBind();
        private void BtnSaveConfig_Click(object sender, EventArgs e) => SaveConfig();

        // ── Inner types ───────────────────────────────────────────────────────────

        private sealed class QuestItem
        {
            public readonly string Id;
            public readonly string Title;

            public QuestItem(string id, string title) { Id = id; Title = title; }
            public override string ToString() => Title;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using OGEditorSDK;

namespace CodeImp.DoomBuilder.UDBScript
{
    /// <summary>
    /// Docker panel for placing cross-game OASIS assets into any OGame map.
    /// Fetches the asset catalog live from the STAR API when reachable;
    /// falls back to the built-in OGAssetCatalog when offline.
    /// </summary>
    public class OGEnginePanel : UserControl
    {
        // ── Config ──────────────────────────────────────────────────────────────────
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UltimateDoomBuilder", "ogengine_config.json");

        private string _apiUrl    = "http://localhost:5000";
        private string _avatarId  = "";

        // ── Live catalog cache ───────────────────────────────────────────────────────
        private readonly Dictionary<string, List<OGAsset>> _liveCache =
            new Dictionary<string, List<OGAsset>>(StringComparer.OrdinalIgnoreCase);
        private bool _liveAvailable;

        // ── Controls ─────────────────────────────────────────────────────────────────
        private readonly BuilderPlug plug;
        private Label    lblStatus;
        private Label    lblGame;
        private ComboBox comboGame;
        private Label    lblCategory;
        private ComboBox comboCategory;
        private Label    lblAsset;
        private ComboBox comboAsset;
        private Button   btnPlace;
        private Button   btnRefresh;
        private Panel    separator;
        private Label    lblConfig;
        private Label    lblUrl;
        private TextBox  txtUrl;
        private Label    lblAvatarId;
        private TextBox  txtAvatarId;
        private Button   btnSaveConfig;

        public OGEnginePanel(BuilderPlug builderPlug)
        {
            plug = builderPlug ?? throw new ArgumentNullException(nameof(builderPlug));
            LoadConfig();
            InitializeComponent();
            ScheduleLiveFetch(SelectedGame());
        }

        // ── Layout ────────────────────────────────────────────────────────────────────

        private void InitializeComponent()
        {
            SuspendLayout();
            BackColor   = SystemColors.Control;
            Padding     = new Padding(6);
            MinimumSize = new Size(200, 400);
            Size        = new Size(260, 460);
            AutoScroll  = true;

            int y = 6;

            lblStatus = new Label
            {
                Text      = "⏳ Connecting to STAR API…",
                AutoSize  = false,
                Width     = 240,
                Height    = 18,
                Location  = new Point(6, y),
                ForeColor = SystemColors.GrayText,
                Font      = new Font(Font.FontFamily, Font.Size - 0.5f),
            };

            y += 22;
            lblGame = new Label { Text = "Game:", AutoSize = true, Location = new Point(6, y) };

            y += 16;
            comboGame = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location      = new Point(6, y),
                Width         = 240,
            };
            foreach (string g in OGameId.All)
                comboGame.Items.Add(g);
            comboGame.SelectedIndex        = 0;
            comboGame.SelectedIndexChanged += ComboGame_SelectedIndexChanged;

            y += 28;
            lblCategory = new Label { Text = "Category:", AutoSize = true, Location = new Point(6, y) };

            y += 16;
            comboCategory = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location      = new Point(6, y),
                Width         = 240,
            };
            comboCategory.SelectedIndexChanged += ComboCategory_SelectedIndexChanged;

            y += 28;
            lblAsset = new Label { Text = "Asset:", AutoSize = true, Location = new Point(6, y) };

            y += 16;
            comboAsset = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location      = new Point(6, y),
                Width         = 240,
                DropDownWidth = 360,
            };

            y += 28;
            btnPlace = new Button
            {
                Text      = "★ Place selected asset at cursor",
                Location  = new Point(6, y),
                Width     = 188,
                Height    = 28,
                FlatStyle = FlatStyle.Standard,
            };
            btnPlace.Click += BtnPlace_Click;

            btnRefresh = new Button
            {
                Text      = "↺",
                Location  = new Point(200, y),
                Width     = 46,
                Height    = 28,
                FlatStyle = FlatStyle.Standard,
                Font      = new Font(Font.FontFamily, Font.Size + 1f),
            };
            btnRefresh.Click    += BtnRefresh_Click;
            new System.Windows.Forms.ToolTip().SetToolTip(btnRefresh, "Refresh catalog from STAR API");

            y += 36;
            separator = new Panel
            {
                Location  = new Point(6, y),
                Width     = 240,
                Height    = 1,
                BackColor = SystemColors.ControlDark,
            };

            y += 8;
            lblConfig = new Label
            {
                Text      = "STAR API Config",
                AutoSize  = true,
                Location  = new Point(6, y),
                Font      = new Font(Font.FontFamily, Font.Size, FontStyle.Bold),
            };

            y += 18;
            lblUrl = new Label { Text = "API URL:", AutoSize = true, Location = new Point(6, y) };

            y += 16;
            txtUrl = new TextBox
            {
                Text     = _apiUrl,
                Location = new Point(6, y),
                Width    = 240,
            };

            y += 26;
            lblAvatarId = new Label { Text = "Avatar ID:", AutoSize = true, Location = new Point(6, y) };

            y += 16;
            txtAvatarId = new TextBox
            {
                Text     = _avatarId,
                Location = new Point(6, y),
                Width    = 240,
            };

            y += 26;
            btnSaveConfig = new Button
            {
                Text      = "Save & Connect",
                Location  = new Point(6, y),
                Width     = 240,
                Height    = 28,
                FlatStyle = FlatStyle.Standard,
            };
            btnSaveConfig.Click += BtnSaveConfig_Click;

            Controls.Add(lblStatus);
            Controls.Add(lblGame);
            Controls.Add(comboGame);
            Controls.Add(lblCategory);
            Controls.Add(comboCategory);
            Controls.Add(lblAsset);
            Controls.Add(comboAsset);
            Controls.Add(btnPlace);
            Controls.Add(btnRefresh);
            Controls.Add(separator);
            Controls.Add(lblConfig);
            Controls.Add(lblUrl);
            Controls.Add(txtUrl);
            Controls.Add(lblAvatarId);
            Controls.Add(txtAvatarId);
            Controls.Add(btnSaveConfig);

            FillCategoryList(GetCurrentAssets(OGameId.ODOOM));
            ResumeLayout(false);
        }

        // ── Config ────────────────────────────────────────────────────────────────────

        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;
                string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                var url = Regex.Match(json, @"""star_api_url""\s*:\s*""([^""]+)""");
                var aid = Regex.Match(json, @"""avatar_id""\s*:\s*""([^""]*)""");
                if (url.Success) _apiUrl   = url.Groups[1].Value;
                if (aid.Success) _avatarId = aid.Groups[1].Value;
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                string json = "{\n  \"star_api_url\": \"" + _apiUrl.Replace("\"", "\\\"") +
                              "\",\n  \"avatar_id\": \""  + _avatarId.Replace("\"", "\\\"") + "\"\n}";
                File.WriteAllText(ConfigPath, json, Encoding.UTF8);
            }
            catch { }
        }

        // ── Live catalog ──────────────────────────────────────────────────────────────

        private void ScheduleLiveFetch(string gameId)
        {
            Task.Run(() =>
            {
                try
                {
                    using (var client = new OGStarApiClient(_apiUrl, _avatarId))
                    {
                        string json = client.GetAssets(gameId);
                        var assets  = ParseAssetsJson(json, gameId);
                        lock (_liveCache) { _liveCache[gameId] = assets; _liveAvailable = true; }
                        BeginInvoke((Action)(() => OnLiveFetchSuccess(gameId)));
                    }
                }
                catch
                {
                    BeginInvoke((Action)(() => OnLiveFetchFailed()));
                }
            });
        }

        private void OnLiveFetchSuccess(string gameId)
        {
            _liveAvailable = true;
            lblStatus.Text      = "⚡ Live — STAR API";
            lblStatus.ForeColor = Color.FromArgb(0, 140, 0);
            if (SelectedGame() == gameId)
                FillCategoryList(GetCurrentAssets(gameId));
        }

        private void OnLiveFetchFailed()
        {
            _liveAvailable = false;
            lblStatus.Text      = "📦 Offline — built-in catalog";
            lblStatus.ForeColor = SystemColors.GrayText;
            FillCategoryList(GetCurrentAssets(SelectedGame()));
        }

        // ── Asset parsing ─────────────────────────────────────────────────────────────

        private static List<OGAsset> ParseAssetsJson(string json, string fallbackGameId)
        {
            var list = new List<OGAsset>();
            if (string.IsNullOrEmpty(json)) return list;

            // Match each JSON object in the array
            foreach (Match obj in Regex.Matches(json, @"\{[^{}]+\}"))
            {
                string o = obj.Value;
                var tt   = Regex.Match(o, @"""thingType""\s*:\s*(\d+)");
                var dn   = Regex.Match(o, @"""displayName""\s*:\s*""([^""]+)""");
                var cat  = Regex.Match(o, @"""category""\s*:\s*""([^""]+)""");
                var gid  = Regex.Match(o, @"""gameId""\s*:\s*""([^""]+)""");
                var nc   = Regex.Match(o, @"""nativeClassname""\s*:\s*""([^""]*)""");

                if (!tt.Success || !dn.Success) continue;

                list.Add(new OGAsset(
                    gid.Success  ? gid.Groups[1].Value  : fallbackGameId,
                    int.Parse(tt.Groups[1].Value),
                    dn.Groups[1].Value,
                    nc.Success   ? nc.Groups[1].Value   : "",
                    cat.Success  ? cat.Groups[1].Value  : "Other"
                ));
            }
            return list;
        }

        private List<OGAsset> GetCurrentAssets(string gameId)
        {
            lock (_liveCache)
            {
                if (_liveAvailable && _liveCache.TryGetValue(gameId, out var live) && live.Count > 0)
                    return live;
            }
            return new List<OGAsset>(OGAssetCatalog.ForGame(gameId));
        }

        // ── UI helpers ────────────────────────────────────────────────────────────────

        private void FillCategoryList(List<OGAsset> assets)
        {
            comboCategory.SelectedIndexChanged -= ComboCategory_SelectedIndexChanged;
            comboCategory.Items.Clear();
            comboCategory.Items.Add("All");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in assets)
                if (seen.Add(a.Category))
                    comboCategory.Items.Add(a.Category);
            comboCategory.SelectedIndex        = 0;
            comboCategory.SelectedIndexChanged += ComboCategory_SelectedIndexChanged;
            FillAssetList(assets, "All");
        }

        private void FillAssetList(List<OGAsset> assets, string category)
        {
            comboAsset.Items.Clear();
            foreach (var a in assets)
                if (category == "All" || string.Equals(a.Category, category, StringComparison.OrdinalIgnoreCase))
                    comboAsset.Items.Add(a);
            if (comboAsset.Items.Count > 0) comboAsset.SelectedIndex = 0;
        }

        private string SelectedGame() => comboGame.SelectedItem?.ToString() ?? OGameId.ODOOM;

        private void SetStatus(string text, Color color)
        {
            lblStatus.Text      = text;
            lblStatus.ForeColor = color;
        }

        // ── Event handlers ────────────────────────────────────────────────────────────

        private void ComboGame_SelectedIndexChanged(object sender, EventArgs e)
        {
            string game   = SelectedGame();
            var    assets = GetCurrentAssets(game);
            FillCategoryList(assets);

            bool cached;
            lock (_liveCache) { cached = _liveCache.ContainsKey(game); }
            if (!cached)
            {
                SetStatus("⏳ Loading catalog…", SystemColors.GrayText);
                ScheduleLiveFetch(game);
            }
        }

        private void ComboCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            string category = comboCategory.SelectedItem?.ToString() ?? "All";
            FillAssetList(GetCurrentAssets(SelectedGame()), category);
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            string game = SelectedGame();
            lock (_liveCache) { _liveCache.Remove(game); }
            SetStatus("⏳ Refreshing…", SystemColors.GrayText);
            ScheduleLiveFetch(game);
        }

        private void BtnSaveConfig_Click(object sender, EventArgs e)
        {
            _apiUrl   = txtUrl.Text.Trim().TrimEnd('/');
            _avatarId = txtAvatarId.Text.Trim();
            SaveConfig();
            lock (_liveCache) { _liveCache.Clear(); _liveAvailable = false; }
            SetStatus("⏳ Connecting…", SystemColors.GrayText);
            ScheduleLiveFetch(SelectedGame());
        }

        private void BtnPlace_Click(object sender, EventArgs e)
        {
            if (General.Map == null)
            {
                MessageBox.Show("Open a map first.", "OGEngine", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var selected = comboAsset.SelectedItem as OGAsset;
            if (selected == null)
            {
                MessageBox.Show("Select an asset first.", "OGEngine", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            plug.SetPendingStarPlacement(selected.ThingType, selected.DisplayName);
        }
    }
}

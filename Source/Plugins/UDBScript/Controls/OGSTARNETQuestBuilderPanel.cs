using System;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace CodeImp.DoomBuilder.UDBScript
{
	/// <summary>
	/// UDB docker panel that embeds the STARNET Quest Builder web app via WebView2 (Chromium/Edge).
	/// Default URL: http://localhost:3000  — configurable via the URL bar.
	/// Requires: Microsoft.Web.WebView2 NuGet package + WebView2 Runtime (ships with Edge / modern Windows).
	/// </summary>
	public sealed class OGSTARNETQuestBuilderPanel : UserControl
	{
		private const string DEFAULT_URL = "http://localhost:3000";

		private TableLayoutPanel layout;
		private Panel toolbar;
		private TextBox urlBox;
		private Button goButton;
		private Button reloadButton;
		private WebView2 webView;
		private Label statusLabel;

		public OGSTARNETQuestBuilderPanel()
		{
			BuildLayout();
			InitWebView();
		}

		private void BuildLayout()
		{
			SuspendLayout();

			// ── Toolbar ──────────────────────────────────────────────────────
			toolbar = new Panel { Dock = DockStyle.Top, Height = 28, Padding = new Padding(2) };

			urlBox = new TextBox
			{
				Text = DEFAULT_URL,
				Dock = DockStyle.Fill,
				BorderStyle = BorderStyle.FixedSingle,
			};
			urlBox.KeyDown += (s, e) =>
			{
				if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Navigate(urlBox.Text); }
			};

			goButton = new Button { Text = "Go", Width = 36, Dock = DockStyle.Right };
			goButton.Click += (s, e) => Navigate(urlBox.Text);

			reloadButton = new Button { Text = "⟳", Width = 28, Dock = DockStyle.Right };
			reloadButton.Click += (s, e) => { try { webView?.Reload(); } catch { } };

			toolbar.Controls.Add(urlBox);
			toolbar.Controls.Add(reloadButton);
			toolbar.Controls.Add(goButton);

			// ── Status label (shown while WebView2 initialises or on error) ──
			statusLabel = new Label
			{
				Text = "Initialising WebView2…",
				Dock = DockStyle.Fill,
				TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
				Visible = true,
			};

			// ── WebView2 ──────────────────────────────────────────────────────
			webView = new WebView2 { Dock = DockStyle.Fill, Visible = false };

			// ── Root layout ───────────────────────────────────────────────────
			layout = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				RowCount = 2,
				ColumnCount = 1,
			};
			layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
			layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
			layout.Controls.Add(toolbar, 0, 0);

			// Stack statusLabel and webView in the same cell; z-order handled by Visible toggling
			var contentPanel = new Panel { Dock = DockStyle.Fill };
			contentPanel.Controls.Add(webView);
			contentPanel.Controls.Add(statusLabel);
			layout.Controls.Add(contentPanel, 0, 1);

			Controls.Add(layout);
			ResumeLayout(false);
		}

		private async void InitWebView()
		{
			try
			{
				await webView.EnsureCoreWebView2Async(null);

				webView.CoreWebView2.NavigationCompleted += (s, e) =>
				{
					if (InvokeRequired) { Invoke(new Action(() => UpdateUrlBar())); }
					else { UpdateUrlBar(); }
				};

				webView.Visible = true;
				statusLabel.Visible = false;

				webView.CoreWebView2.Navigate(DEFAULT_URL);
			}
			catch (Exception ex)
			{
				statusLabel.Text =
					"WebView2 could not be initialised.\r\n\r\n" +
					"Make sure the WebView2 Runtime is installed\r\n" +
					"(it ships with Microsoft Edge on modern Windows).\r\n\r\n" +
					ex.Message;
				statusLabel.Visible = true;
				webView.Visible = false;
			}
		}

		private void Navigate(string url)
		{
			if (webView?.CoreWebView2 == null) return;
			if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
				!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
				url = "http://" + url;
			try { webView.CoreWebView2.Navigate(url); }
			catch (Exception ex) { statusLabel.Text = "Navigation error: " + ex.Message; }
		}

		private void UpdateUrlBar()
		{
			if (webView?.CoreWebView2 != null)
				urlBox.Text = webView.CoreWebView2.Source;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				webView?.Dispose();
				layout?.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}

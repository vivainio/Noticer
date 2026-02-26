using System.Drawing;
using System.Runtime.InteropServices;

namespace Noticer;

public class MainForm : Form
{
    private readonly NotificationListener _listener;
    private readonly FlowLayoutPanel _listPanel;
    private readonly Button _clearButton;
    private readonly NotifyIcon _trayIcon;
    private int _unreadCount = 0;

    // source name → group panel
    private readonly Dictionary<string, SourceGroup> _sourceGroups = new();

    private static readonly Dictionary<string, Color> LevelColors = new()
    {
        ["info"]    = Color.FromArgb(220, 235, 252),
        ["warn"]    = Color.FromArgb(255, 243, 205),
        ["warning"] = Color.FromArgb(255, 243, 205),
        ["error"]   = Color.FromArgb(252, 220, 220),
        ["success"] = Color.FromArgb(212, 237, 218),
    };

    private static readonly Dictionary<string, Color> LevelBorderColors = new()
    {
        ["info"]    = Color.FromArgb(100, 160, 230),
        ["warn"]    = Color.FromArgb(230, 180, 50),
        ["warning"] = Color.FromArgb(230, 180, 50),
        ["error"]   = Color.FromArgb(220, 80, 80),
        ["success"] = Color.FromArgb(60, 180, 100),
    };

    public MainForm()
    {
        Text = "Noticer";
        Size = new Size(480, 600);
        MinimumSize = new Size(300, 300);
        Font = new Font("Segoe UI", 9f);
        // Use a specific color as the transparency key so the background
        // becomes click-through while the cards remain fully opaque.
        var chromaKey = Color.FromArgb(1, 2, 3);
        BackColor = chromaKey;
        TransparencyKey = chromaKey;

        // ── Toolbar ──────────────────────────────────────────────
        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(40, 40, 50),
        };

        var titleLabel = new Label
        {
            Text = "Noticer",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Top = 0,
            Height = 44,
            Left = 12,
        };

        _clearButton = new Button
        {
            Text = "Clear all",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(80, 80, 100),
            Size = new Size(80, 28),
            Top = 8,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            FlatAppearance = { BorderColor = Color.FromArgb(120, 120, 140) },
        };
        _clearButton.Left = toolbar.ClientSize.Width - _clearButton.Width - 8;
        _clearButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        _clearButton.Click += (_, _) => ClearAll();

        var pinButton = new Button
        {
            Text = "📌",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(160, 160, 180),
            BackColor = Color.FromArgb(80, 80, 100),
            Size = new Size(36, 28),
            Top = 8,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            FlatAppearance = { BorderColor = Color.FromArgb(120, 120, 140) },
        };
        pinButton.Left = toolbar.ClientSize.Width - _clearButton.Width - pinButton.Width - 16;
        pinButton.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        pinButton.Click += (_, _) =>
        {
            TopMost = !TopMost;
            pinButton.ForeColor = TopMost ? Color.White : Color.FromArgb(160, 160, 180);
            pinButton.BackColor = TopMost ? Color.FromArgb(80, 100, 140) : Color.FromArgb(80, 80, 100);
        };

        toolbar.Controls.AddRange([titleLabel, pinButton, _clearButton]);
        Controls.Add(toolbar);

        // ── Scrollable notification list ─────────────────────────
        // Explicitly positioned below the toolbar instead of Dock=Fill
        // to avoid scroll starting at y=0 under the toolbar.
        const int toolbarH = 44;
        var scroll = new Panel
        {
            Location = new Point(0, toolbarH),
            Size = new Size(ClientSize.Width, ClientSize.Height - toolbarH),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AutoScroll = true,
            BackColor = chromaKey,
        };

        _listPanel = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Top,
            Padding = new Padding(8, 8, 8, 8),
            BackColor = chromaKey,
        };

        scroll.Controls.Add(_listPanel);
        Controls.Add(scroll);

        // ── Tray icon ────────────────────────────────────────────
        _trayIcon = new NotifyIcon
        {
            Text = "Noticer",
            Icon = SystemIcons.Information,
            Visible = true,
        };
        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Show", null, (_, _) => ShowWindow());
        trayMenu.Items.Add("Exit", null, (_, _) => { _trayIcon.Visible = false; Application.Exit(); });
        _trayIcon.ContextMenuStrip = trayMenu;
        _trayIcon.DoubleClick += (_, _) => ShowWindow();

        // ── Listener ─────────────────────────────────────────────
        _listener = new NotificationListener();
        _listener.NotificationReceived += OnNotificationReceived;

        try { _listener.Start(); }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not start listener on port {NotificationListener.Port}:\n{ex.Message}",
                "Noticer", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        MouseDown += OnDragMouseDown;
        scroll.MouseDown += OnDragMouseDown;
        _listPanel.MouseDown += OnDragMouseDown;

        FormClosing += OnFormClosing;
        Resize += OnResize;
    }

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private void OnDragMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, 0xA1 /* WM_NCLBUTTONDOWN */, 2 /* HTCAPTION */, 0);
        }
    }

    private void OnNotificationReceived(NotificationItem item)
    {
        if (InvokeRequired) Invoke(() => AddNotification(item));
        else AddNotification(item);
    }

    private static void RemoveDuplicate(FlowLayoutPanel panel, NotificationItem item)
    {
        var dupe = panel.Controls.OfType<Panel>()
            .FirstOrDefault(c => c.Tag is NotificationItem n
                && n.Title == item.Title && n.Message == item.Message);
        if (dupe != null) panel.Controls.Remove(dupe);
    }

    private void AddNotification(NotificationItem item)
    {
        _unreadCount++;
        UpdateTrayText();

        var card = BuildCard(item, indent: !string.IsNullOrWhiteSpace(item.Source));

        if (string.IsNullOrWhiteSpace(item.Source))
        {
            RemoveDuplicate(_listPanel, item);
            InsertAtTop(_listPanel, card);
        }
        else
        {
            var source = item.Source!;
            if (!_sourceGroups.TryGetValue(source, out var group))
            {
                group = new SourceGroup(source, _listPanel.ClientSize.Width - 16);
                _sourceGroups[source] = group;
                InsertAtTop(_listPanel, group.Container);
            }
            group.AddCard(card);
        }

        _listPanel.Width = _listPanel.Parent?.ClientSize.Width ?? _listPanel.Width;
    }

    private Panel BuildCard(NotificationItem item, bool indent)
    {
        var level = item.Level.ToLowerInvariant();
        var bgColor = LevelColors.GetValueOrDefault(level, Color.FromArgb(235, 235, 240));
        var borderColor = LevelBorderColors.GetValueOrDefault(level, Color.LightGray);

        int cardWidth = _listPanel.ClientSize.Width - (indent ? 36 : 16);

        var card = new Panel
        {
            Width = cardWidth,
            Height = 76,
            Margin = new Padding(indent ? 20 : 0, 0, 0, 4),
            BackColor = bgColor,
            BorderStyle = BorderStyle.FixedSingle,
        };

        var levelBadge = new Label
        {
            Text = item.Level.ToUpperInvariant(),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = borderColor,
            AutoSize = true,
            Location = new Point(8, 5),
        };

        var timeLabel = new Label
        {
            Text = item.ReceivedAt.ToString("HH:mm:ss"),
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = Color.Gray,
            AutoSize = true,
        };
        timeLabel.Location = new Point(card.Width - timeLabel.PreferredWidth - 8, 5);
        timeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        var titleLabel = new Label
        {
            Text = item.Title,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 30, 40),
            AutoSize = false,
            Location = new Point(8, 22),
            Size = new Size(card.Width - 16, 20),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
        };

        var msgLabel = new Label
        {
            Text = item.Message,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(60, 60, 70),
            AutoSize = false,
            Location = new Point(8, 42),
            Size = new Size(card.Width - 16, 28),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
        };

        card.Controls.AddRange([levelBadge, timeLabel, titleLabel, msgLabel]);
        return card;
    }

    private static void InsertAtTop(FlowLayoutPanel panel, Control control)
    {
        panel.SuspendLayout();
        panel.Controls.Add(control);
        panel.Controls.SetChildIndex(control, 0);
        panel.ResumeLayout();
    }

    private void ClearAll()
    {
        _listPanel.Controls.Clear();
        _sourceGroups.Clear();
        _unreadCount = 0;
        UpdateTrayText();
    }

    private void UpdateTrayText()
    {
        _trayIcon.Text = _unreadCount > 0 ? $"Noticer ({_unreadCount})" : "Noticer";
    }

    private void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        _unreadCount = 0;
        UpdateTrayText();
    }

    private void OnResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized) Hide();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _listener.Dispose(); _trayIcon.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>A collapsible group container for notifications from the same source.</summary>
internal class SourceGroup
{
    public FlowLayoutPanel Container { get; }
    private readonly Panel _header;
    private readonly FlowLayoutPanel _body;
    private bool _collapsed = false;
    private int _count = 0;
    private readonly Label _countLabel;

    public SourceGroup(string source, int width)
    {
        _body = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = width,
            Padding = new Padding(0, 2, 0, 4),
            Visible = true,
        };

        _header = new Panel
        {
            Width = width,
            Height = 32,
            BackColor = Color.FromArgb(55, 55, 70),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 0),
        };

        var arrow = new Label
        {
            Text = "▾",
            ForeColor = Color.FromArgb(180, 180, 200),
            Font = new Font("Segoe UI", 9f),
            AutoSize = true,
            Location = new Point(8, 7),
        };

        var nameLabel = new Label
        {
            Text = source,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(26, 7),
        };

        _countLabel = new Label
        {
            Text = "0",
            ForeColor = Color.FromArgb(160, 160, 180),
            Font = new Font("Segoe UI", 8f),
            AutoSize = true,
        };

        _header.Controls.AddRange([arrow, nameLabel, _countLabel]);

        EventHandler toggleCollapse = (_, _) =>
        {
            _collapsed = !_collapsed;
            _body.Visible = !_collapsed;
            arrow.Text = _collapsed ? "▸" : "▾";
        };
        _header.Click += toggleCollapse;
        foreach (Control c in _header.Controls)
            c.Click += toggleCollapse;

        Container = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = width,
            Margin = new Padding(0, 0, 0, 6),
        };
        Container.Controls.Add(_header);
        Container.Controls.Add(_body);
    }

    public void AddCard(Panel card)
    {
        // Remove existing card with same title+message
        if (card.Tag is NotificationItem item)
        {
            var dupe = _body.Controls.OfType<Panel>()
                .FirstOrDefault(c => c.Tag is NotificationItem n
                    && n.Title == item.Title && n.Message == item.Message);
            if (dupe != null) { _body.Controls.Remove(dupe); _count--; }
        }

        _body.SuspendLayout();
        _body.Controls.Add(card);
        _body.Controls.SetChildIndex(card, 0);
        _body.ResumeLayout();

        _count++;
        _countLabel.Text = $"({_count})";
        _countLabel.Left = _header.Width - _countLabel.PreferredWidth - 10;
    }
}

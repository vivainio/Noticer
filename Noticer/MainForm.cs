using System.Drawing;
using System.Runtime.InteropServices;

namespace Noticer;

public class MainForm : Form
{
    private readonly NotificationListener _listener;
    private readonly FlowLayoutPanel _listPanel;
    private readonly Button _clearButton;
    private readonly Icon _appIcon;
    private readonly Panel _bottomEdge;
    private readonly List<(Label Label, NotificationItem Item)> _timeLabels = new();

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
        _appIcon = CreateAppIcon();
        Text = "Noticer";
        Icon = _appIcon;
        FormBorderStyle = FormBorderStyle.Sizable;
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
            Height = 36,
            BackColor = chromaKey,
        };

        _clearButton = new Button
        {
            Text = "🗑",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(160, 160, 180),
            BackColor = Color.FromArgb(80, 80, 100),
            Size = new Size(36, 28),
            Top = 4,
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
            Top = 4,
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
            FormBorderStyle = TopMost ? FormBorderStyle.None : FormBorderStyle.Sizable;
            _bottomEdge.Visible = TopMost;
            if (TopMost) _bottomEdge.BringToFront();
        };

        toolbar.Controls.AddRange([pinButton, _clearButton]);
        Controls.Add(toolbar);

        // ── Scrollable notification list ─────────────────────────
        // Explicitly positioned below the toolbar instead of Dock=Fill
        // to avoid scroll starting at y=0 under the toolbar.
        const int toolbarH = 36;
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

        _bottomEdge = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 3,
            BackColor = Color.FromArgb(80, 100, 160),
            Visible = false,
        };
        Controls.Add(_bottomEdge);

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

        var ticker = new System.Windows.Forms.Timer { Interval = 30_000 };
        ticker.Tick += (_, _) => RefreshTimeLabels();
        ticker.Start();

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

    private static void RemoveTransients(FlowLayoutPanel panel)
    {
        foreach (var c in panel.Controls.OfType<Panel>()
            .Where(c => c.Tag is NotificationItem n && n.Transient).ToList())
            panel.Controls.Remove(c);
    }

    private void AddNotification(NotificationItem item)
    {
        var card = BuildCard(item);

        if (string.IsNullOrWhiteSpace(item.Source))
        {
            RemoveTransients(_listPanel);
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

    private Panel BuildCard(NotificationItem item)
    {
        var level = item.Level.ToLowerInvariant();
        var bgColor = LevelColors.GetValueOrDefault(level, Color.FromArgb(235, 235, 240));
        var borderColor = LevelBorderColors.GetValueOrDefault(level, Color.LightGray);

        int cardWidth = _listPanel.ClientSize.Width - 16;

        if (item.Slim)
        {
            var card = new Panel
            {
                Width = cardWidth,
                Height = 28,
                Margin = new Padding(0, 0, 0, 2),
                BackColor = bgColor,
                BorderStyle = BorderStyle.FixedSingle,
                Tag = item,
            };

            var levelBadge = new Label
            {
                Text = item.Level.ToUpperInvariant(),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = borderColor,
                AutoSize = true,
                Location = new Point(6, 6),
            };

            var titleLabel = new Label
            {
                Text = string.IsNullOrEmpty(item.Message) ? item.Title : $"{item.Title}: {item.Message}",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(30, 30, 40),
                AutoSize = true,
                Location = new Point(levelBadge.Left + levelBadge.PreferredWidth + 6, 6),
            };

            var timeLabel = new Label
            {
                Text = HumanizeAge(item.ReceivedAt),
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.Gray,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            timeLabel.Location = new Point(card.Width - timeLabel.PreferredWidth - 6, 7);
            _timeLabels.Add((timeLabel, item));

            card.Controls.AddRange([levelBadge, titleLabel, timeLabel]);
            return card;
        }

        var card2 = new Panel
        {
            Width = cardWidth,
            Height = 76,
            Margin = new Padding(0, 0, 0, 4),
            BackColor = bgColor,
            BorderStyle = BorderStyle.FixedSingle,
            Tag = item,
        };

        var levelBadge2 = new Label
        {
            Text = item.Level.ToUpperInvariant(),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            ForeColor = borderColor,
            AutoSize = true,
            Location = new Point(8, 5),
        };

        var timeLabel2 = new Label
        {
            Text = HumanizeAge(item.ReceivedAt),
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = Color.Gray,
            AutoSize = true,
        };
        timeLabel2.Location = new Point(card2.Width - timeLabel2.PreferredWidth - 8, 5);
        timeLabel2.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _timeLabels.Add((timeLabel2, item));

        var titleLabel2 = new Label
        {
            Text = item.Title,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 30, 40),
            AutoSize = false,
            Location = new Point(8, 22),
            Size = new Size(card2.Width - 16, 20),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
        };

        var msgLabel2 = new Label
        {
            Text = item.Message,
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(60, 60, 70),
            AutoSize = false,
            Location = new Point(8, 42),
            Size = new Size(card2.Width - 16, 28),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
        };

        card2.Controls.AddRange([levelBadge2, timeLabel2, titleLabel2, msgLabel2]);
        return card2;
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
        _timeLabels.Clear();
    }


    private void OnResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized) return;
        ResizeCards();
    }

    private void ResizeCards()
    {
        if (_listPanel.Parent == null) return;
        int newWidth = _listPanel.Parent.ClientSize.Width;
        _listPanel.Width = newWidth;

        foreach (Control c in _listPanel.Controls)
        {
            if (c.Tag is NotificationItem)
                c.Width = newWidth - 16;
            else if (_sourceGroups.Values.FirstOrDefault(g => g.Container == c) is SourceGroup group)
                group.Resize(newWidth);
        }
    }


    private static string HumanizeAge(DateTime t)
    {
        var age = DateTime.Now - t;
        if (age.TotalMinutes < 1)   return "";
        if (age.TotalHours < 1)     return $"{(int)age.TotalMinutes}m ago";
        if (age.TotalDays < 1)      return $"{(int)age.TotalHours}h ago";
        return $"{(int)age.TotalDays}d ago";
    }

    private void RefreshTimeLabels()
    {
        foreach (var (label, item) in _timeLabels)
            label.Text = HumanizeAge(item.ReceivedAt);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _listener.Dispose(); _appIcon.Dispose(); }
        base.Dispose(disposing);
    }

    private static Icon CreateAppIcon()
    {
        using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Blue rounded-square background
        using var bgBrush = new SolidBrush(Color.FromArgb(60, 110, 220));
        using var bgPath = RoundedRect(new Rectangle(0, 0, 32, 32), 7);
        g.FillPath(bgBrush, bgPath);

        // White bell shape
        using var wb = new SolidBrush(Color.White);

        // Handle (small oval at top)
        g.FillEllipse(wb, 13, 3, 6, 4);

        // Bell dome (filled arc + trapezoid body)
        using var bellPath = new System.Drawing.Drawing2D.GraphicsPath();
        bellPath.AddArc(7, 6, 18, 15, 180, 180);
        bellPath.AddLine(25, 14, 26, 21);
        bellPath.AddLine(26, 21, 6, 21);
        bellPath.AddLine(6, 21, 7, 14);
        bellPath.CloseFigure();
        g.FillPath(wb, bellPath);

        // Bottom rim
        g.FillRectangle(wb, 5, 20, 22, 3);

        // Clapper
        g.FillEllipse(wb, 13, 23, 6, 5);

        return Icon.FromHandle(bmp.GetHicon());
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle b, int r)
    {
        var p = new System.Drawing.Drawing2D.GraphicsPath();
        int d = r * 2;
        p.AddArc(b.X, b.Y, d, d, 180, 90);
        p.AddArc(b.Right - d, b.Y, d, d, 270, 90);
        p.AddArc(b.Right - d, b.Bottom - d, d, d, 0, 90);
        p.AddArc(b.X, b.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
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

    public void Resize(int listWidth)
    {
        int groupWidth = listWidth - 16;
        Container.Width = groupWidth;
        _header.Width = groupWidth;
        _countLabel.Left = groupWidth - _countLabel.PreferredWidth - 10;
        _body.Width = groupWidth;
        foreach (var card in _body.Controls.OfType<Panel>())
            card.Width = listWidth - 16;
    }

    public void AddCard(Panel card)
    {
        // Remove transients and duplicates before inserting
        var transients = _body.Controls.OfType<Panel>()
            .Where(c => c.Tag is NotificationItem n && n.Transient).ToList();
        foreach (var t in transients) { _body.Controls.Remove(t); _count--; }

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

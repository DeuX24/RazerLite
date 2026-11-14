using System.Drawing;
using System.Windows.Forms;

namespace RazerLite;

internal sealed class MainForm : Form
{
    private static readonly Color BgWindow = Color.FromArgb(18, 19, 22);
    private static readonly Color BgCard = Color.FromArgb(28, 30, 36);
    private static readonly Color BgButton = Color.FromArgb(38, 42, 50);
    private static readonly Color BorderSubtle = Color.FromArgb(46, 51, 61);
    private static readonly Color AccentGreen = Color.FromArgb(0, 230, 91);
    private static readonly Color AccentGreenDark = Color.FromArgb(0, 175, 70);
    private static readonly Color TextPrimary = Color.FromArgb(240, 242, 245);
    private static readonly Color TextMuted = Color.FromArgb(154, 160, 172);
    private static readonly Color StatusWarn = Color.FromArgb(255, 193, 7);

    private readonly AppConfig _config;
    private readonly float _scale;
    private readonly Font _fontBase;
    private readonly Font _fontTitle;
    private readonly Font _fontSub;
    private readonly Font _fontSection;
    private readonly Font _fontToggleNormal;
    private readonly Font _fontToggleSelected;
    private readonly Font _fontActionBold;

    private List<HidDeviceInfo> _devices = [];
    private bool _syncingUi;

    private Label _lblDevices = null!;
    private Label _lblStatus = null!;
    private Button _btnSaveAll = null!;

    private Panel _emptyStateCard = null!;
    private Panel _mouseCard = null!;
    private Panel _kbdCard = null!;

    private CheckBox _chkMouseEnabled = null!;
    private CheckBox _chkMouseStartup = null!;
    private readonly List<Control> _mouseInnerControls = [];
    private readonly Dictionary<int, Button> _dpiButtons = new();
    private readonly Dictionary<int, Button> _pollButtons = new();
    private CheckBox _chkScrollLed = null!;
    private CheckBox _chkLogoLed = null!;

    private CheckBox _chkKbdEnabled = null!;
    private CheckBox _chkKbdStartup = null!;
    private readonly List<Control> _kbdInnerControls = [];
    private readonly Dictionary<KeyboardEffect, Button> _effectButtons = new();
    private Panel _colorPreview = null!;
    private TrackBar _trkBrightness = null!;
    private Label _lblBrightnessVal = null!;

    public MainForm(AppConfig config)
    {
        _config = config;
        using (Graphics g = CreateGraphics())
        {
            _scale = Math.Max(1.0f, g.DpiX / 96.0f);
        }

        _fontBase = SFont(13f);
        _fontTitle = SFont(22f, FontStyle.Bold);
        _fontSub = SFont(12f);
        _fontSection = SFont(14.5f, FontStyle.Bold);
        _fontToggleNormal = SFont(12.5f);
        _fontToggleSelected = SFont(12.5f, FontStyle.Bold);
        _fontActionBold = SFont(13f, FontStyle.Bold);

        InitializeForm();
        BuildLayout();
        SyncUiFromConfig();
        RefreshDevices();
    }

    private int S(int px) => (int)Math.Round(px * _scale);
    private Point SP(int x, int y) => new(S(x), S(y));
    private Size SS(int w, int h) => new(S(w), S(h));
    private Font SFont(float px, FontStyle style = FontStyle.Regular) =>
        new("Segoe UI", px * _scale, style, GraphicsUnit.Pixel);

    private void InitializeForm()
    {
        Text = "RazerLite";
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = SS(680, 320);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgWindow;
        ForeColor = TextPrimary;
        Font = _fontBase;
    }

    private void BuildLayout()
    {
        const int pad = 20;
        const int cardWidth = 640;

        var lblTitle = new Label
        {
            Text = "RazerLite",
            Font = _fontTitle,
            ForeColor = AccentGreen,
            AutoSize = false,
            Size = SS(240, 30),
            Location = SP(pad, 12),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(lblTitle);

        var lblSub = new Label
        {
            Text = "Lightweight hardware control  •  Zero background daemon",
            Font = _fontSub,
            ForeColor = TextMuted,
            AutoSize = false,
            Size = SS(440, 20),
            Location = SP(pad + 2, 44),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(lblSub);

        Button btnRefresh = CreateActionButton("Refresh Devices", SP(680 - pad - 140, 18), SS(140, 36), secondary: true);
        btnRefresh.Click += (_, _) => RefreshDevices();
        Controls.Add(btnRefresh);

        Panel deviceBanner = CreateCard(SP(pad, 74), SS(cardWidth, 44));
        _lblDevices = new Label
        {
            Text = "Scanning USB HID devices...",
            Font = _fontSub,
            ForeColor = TextMuted,
            AutoSize = false,
            Size = SS(cardWidth - 24, 28),
            Location = SP(12, 8),
            TextAlign = ContentAlignment.MiddleLeft
        };
        deviceBanner.Controls.Add(_lblDevices);
        Controls.Add(deviceBanner);

        _emptyStateCard = CreateCard(SP(pad, 130), SS(cardWidth, 108));
        _emptyStateCard.Controls.Add(new Label
        {
            Text = "No supported Razer devices detected",
            Font = _fontSection,
            ForeColor = TextPrimary,
            AutoSize = false,
            Size = SS(cardWidth - 32, 26),
            Location = SP(16, 20),
            TextAlign = ContentAlignment.MiddleLeft
        });
        _emptyStateCard.Controls.Add(new Label
        {
            Text = "Plug in your Razer DeathAdder Left-Hand (3.5G) or BlackWidow keyboard and click Refresh Devices.\nOnly connected devices are shown here.",
            Font = _fontSub,
            ForeColor = TextMuted,
            AutoSize = false,
            Size = SS(cardWidth - 32, 44),
            Location = SP(16, 48),
            TextAlign = ContentAlignment.TopLeft
        });
        Controls.Add(_emptyStateCard);

        _mouseCard = CreateCard(SP(pad, 130), SS(cardWidth, 212));
        AddMouseControls(_mouseCard);
        Controls.Add(_mouseCard);

        _kbdCard = CreateCard(SP(pad, 354), SS(cardWidth, 228));
        AddKeyboardControls(_kbdCard);
        Controls.Add(_kbdCard);

        _btnSaveAll = CreateActionButton("Save && Apply Enabled", SP(680 - pad - 185, 594), SS(185, 36), secondary: false);
        _btnSaveAll.Click += (_, _) => ApplyAll();
        Controls.Add(_btnSaveAll);

        _lblStatus = new Label
        {
            Text = "Ready.",
            Font = _fontSub,
            ForeColor = TextMuted,
            AutoSize = false,
            Size = SS(cardWidth, 46),
            Location = SP(pad, 638),
            TextAlign = ContentAlignment.TopLeft
        };
        Controls.Add(_lblStatus);
    }

    private void AddMouseControls(Panel card)
    {
        AddSectionHeader(card, "DeathAdder Left-Hand Edition (3.5G  •  PID 0016)");

        _chkMouseEnabled = new CheckBox
        {
            Text = "Enable control",
            Font = _fontSub,
            ForeColor = TextPrimary,
            AutoSize = false,
            Size = SS(130, 24),
            Location = SP(494, 12),
            CheckAlign = ContentAlignment.MiddleLeft
        };
        _chkMouseEnabled.CheckedChanged += (_, _) =>
        {
            if (_syncingUi) return;
            _config.MouseEnabled = _chkMouseEnabled.Checked;
            _config.Save();
            _config.SyncStartupRegistry();
            SyncUiFromConfig();
            SetStatus(_config.MouseEnabled ? "Mouse control enabled." : "Mouse control disabled.", true);
        };
        card.Controls.Add(_chkMouseEnabled);

        Label lblDpi = CreateFieldLabel("DPI", SP(16, 48));
        card.Controls.Add(lblDpi);
        _mouseInnerControls.Add(lblDpi);

        int[] dpis = [450, 900, 1800, 3500];
        for (int i = 0; i < dpis.Length; i++)
        {
            int dpi = dpis[i];
            Button btn = CreateToggleButton($"{dpi}", SP(128 + (i * 86), 43), SS(78, 34));
            btn.Click += (_, _) =>
            {
                _config.MouseDpi = dpi;
                SyncUiFromConfig();
            };
            _dpiButtons[dpi] = btn;
            card.Controls.Add(btn);
            _mouseInnerControls.Add(btn);
        }

        Label lblPoll = CreateFieldLabel("Polling Rate", SP(16, 92));
        card.Controls.Add(lblPoll);
        _mouseInnerControls.Add(lblPoll);

        int[] polls = [125, 500, 1000];
        for (int i = 0; i < polls.Length; i++)
        {
            int poll = polls[i];
            Button btn = CreateToggleButton($"{poll} Hz", SP(128 + (i * 112), 87), SS(104, 34));
            btn.Click += (_, _) =>
            {
                _config.MousePollRateHz = poll;
                SyncUiFromConfig();
            };
            _pollButtons[poll] = btn;
            card.Controls.Add(btn);
            _mouseInnerControls.Add(btn);
        }

        Label lblLeds = CreateFieldLabel("Mouse LEDs", SP(16, 136));
        card.Controls.Add(lblLeds);
        _mouseInnerControls.Add(lblLeds);

        _chkScrollLed = new CheckBox
        {
            Text = "Scroll Wheel",
            Font = _fontBase,
            ForeColor = TextPrimary,
            AutoSize = false,
            Size = SS(122, 26),
            Location = SP(128, 135)
        };
        _chkScrollLed.CheckedChanged += (_, _) =>
        {
            if (!_syncingUi) _config.MouseScrollLed = _chkScrollLed.Checked;
        };
        card.Controls.Add(_chkScrollLed);
        _mouseInnerControls.Add(_chkScrollLed);

        _chkLogoLed = new CheckBox
        {
            Text = "Logo",
            Font = _fontBase,
            ForeColor = TextPrimary,
            AutoSize = false,
            Size = SS(90, 26),
            Location = SP(260, 135)
        };
        _chkLogoLed.CheckedChanged += (_, _) =>
        {
            if (!_syncingUi) _config.MouseLogoLed = _chkLogoLed.Checked;
        };
        card.Controls.Add(_chkLogoLed);
        _mouseInnerControls.Add(_chkLogoLed);

        _chkMouseStartup = new CheckBox
        {
            Text = "Apply mouse settings on Windows startup",
            Font = _fontSub,
            ForeColor = TextMuted,
            AutoSize = false,
            Size = SS(330, 26),
            Location = SP(16, 174)
        };
        _chkMouseStartup.CheckedChanged += (_, _) =>
        {
            if (_syncingUi) return;
            _config.MouseApplyOnStartup = _chkMouseStartup.Checked;
            _config.Save();
            UpdateStartupRegistry("Mouse");
        };
        card.Controls.Add(_chkMouseStartup);
        _mouseInnerControls.Add(_chkMouseStartup);

        Button btnApplyMouse = CreateActionButton("Apply Mouse", SP(484, 166), SS(140, 34), secondary: true);
        btnApplyMouse.Click += (_, _) => ApplyMouseOnly();
        card.Controls.Add(btnApplyMouse);
        _mouseInnerControls.Add(btnApplyMouse);
    }

    private void AddKeyboardControls(Panel card)
    {
        AddSectionHeader(card, "BlackWidow Chroma V2 (PID 0221)");

        _chkKbdEnabled = new CheckBox
        {
            Text = "Enable control",
            Font = _fontSub,
            ForeColor = TextPrimary,
            AutoSize = false,
            Size = SS(130, 24),
            Location = SP(494, 12),
            CheckAlign = ContentAlignment.MiddleLeft
        };
        _chkKbdEnabled.CheckedChanged += (_, _) =>
        {
            if (_syncingUi) return;
            _config.KeyboardEnabled = _chkKbdEnabled.Checked;
            _config.Save();
            _config.SyncStartupRegistry();
            SyncUiFromConfig();
            SetStatus(_config.KeyboardEnabled ? "Keyboard control enabled." : "Keyboard control disabled.", true);
        };
        card.Controls.Add(_chkKbdEnabled);

        Label lblEff = CreateFieldLabel("Effect", SP(16, 48));
        card.Controls.Add(lblEff);
        _kbdInnerControls.Add(lblEff);

        (KeyboardEffect Effect, string Label)[] effects =
        [
            (KeyboardEffect.Static, "Static"),
            (KeyboardEffect.Spectrum, "Spectrum"),
            (KeyboardEffect.WaveLeft, "Wave L"),
            (KeyboardEffect.WaveRight, "Wave R"),
            (KeyboardEffect.Breathing, "Breathing"),
            (KeyboardEffect.Off, "Off")
        ];

        for (int i = 0; i < effects.Length; i++)
        {
            KeyboardEffect eff = effects[i].Effect;
            Button btn = CreateToggleButton(effects[i].Label, SP(128 + (i * 83), 43), SS(77, 34));
            btn.Click += (_, _) =>
            {
                _config.KeyboardEffect = eff;
                SyncUiFromConfig();
            };
            _effectButtons[eff] = btn;
            card.Controls.Add(btn);
            _kbdInnerControls.Add(btn);
        }

        Label lblColor = CreateFieldLabel("Color", SP(16, 94));
        card.Controls.Add(lblColor);
        _kbdInnerControls.Add(lblColor);

        Color[] swatches =
        [
            Color.FromArgb(0, 230, 91),   // Razer Green
            Color.FromArgb(0, 174, 255),  // Ice Blue
            Color.FromArgb(255, 50, 50),  // Crimson
            Color.FromArgb(255, 165, 0),  // Amber
            Color.FromArgb(170, 70, 255), // Violet
            Color.FromArgb(255, 255, 255) // White
        ];

        for (int i = 0; i < swatches.Length; i++)
        {
            Color c = swatches[i];
            var swatch = new Button
            {
                BackColor = c,
                FlatStyle = FlatStyle.Flat,
                Size = SS(28, 28),
                Location = SP(128 + (i * 36), 90),
                Cursor = Cursors.Hand
            };
            swatch.FlatAppearance.BorderSize = 1;
            swatch.FlatAppearance.BorderColor = BorderSubtle;
            swatch.Click += (_, _) =>
            {
                _config.ColorR = c.R;
                _config.ColorG = c.G;
                _config.ColorB = c.B;
                SyncUiFromConfig();
            };
            card.Controls.Add(swatch);
            _kbdInnerControls.Add(swatch);
        }

        _colorPreview = new Panel
        {
            Size = SS(28, 28),
            Location = SP(354, 90),
            BorderStyle = BorderStyle.FixedSingle
        };
        card.Controls.Add(_colorPreview);
        _kbdInnerControls.Add(_colorPreview);

        Button btnPickColor = CreateActionButton("Custom...", SP(392, 88), SS(96, 32), secondary: true);
        btnPickColor.Click += (_, _) => PickCustomColor();
        card.Controls.Add(btnPickColor);
        _kbdInnerControls.Add(btnPickColor);

        Label lblBright = CreateFieldLabel("Brightness", SP(16, 140));
        card.Controls.Add(lblBright);
        _kbdInnerControls.Add(lblBright);

        _trkBrightness = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 25,
            AutoSize = false,
            Size = SS(260, 34),
            Location = SP(124, 134),
            BackColor = BgCard
        };
        _trkBrightness.ValueChanged += (_, _) =>
        {
            if (_syncingUi) return;
            _config.KeyboardBrightness = _trkBrightness.Value;
            _lblBrightnessVal.Text = $"{_config.KeyboardBrightness}%";
        };
        card.Controls.Add(_trkBrightness);
        _kbdInnerControls.Add(_trkBrightness);

        _lblBrightnessVal = new Label
        {
            Text = "80%",
            Font = _fontBase,
            ForeColor = TextPrimary,
            AutoSize = false,
            Size = SS(54, 24),
            Location = SP(392, 140),
            TextAlign = ContentAlignment.MiddleLeft
        };
        card.Controls.Add(_lblBrightnessVal);
        _kbdInnerControls.Add(_lblBrightnessVal);

        _chkKbdStartup = new CheckBox
        {
            Text = "Apply keyboard settings on Windows startup",
            Font = _fontSub,
            ForeColor = TextMuted,
            AutoSize = false,
            Size = SS(340, 26),
            Location = SP(16, 188)
        };
        _chkKbdStartup.CheckedChanged += (_, _) =>
        {
            if (_syncingUi) return;
            _config.KeyboardApplyOnStartup = _chkKbdStartup.Checked;
            _config.Save();
            UpdateStartupRegistry("Keyboard");
        };
        card.Controls.Add(_chkKbdStartup);
        _kbdInnerControls.Add(_chkKbdStartup);

        Button btnApplyKbd = CreateActionButton("Apply Keyboard", SP(484, 182), SS(140, 34), secondary: true);
        btnApplyKbd.Click += (_, _) => ApplyKeyboardOnly();
        card.Controls.Add(btnApplyKbd);
        _kbdInnerControls.Add(btnApplyKbd);
    }

    private void SyncUiFromConfig()
    {
        _syncingUi = true;
        try
        {
            _chkMouseEnabled.Checked = _config.MouseEnabled;
            _chkMouseStartup.Checked = _config.MouseApplyOnStartup;
            foreach (KeyValuePair<int, Button> kvp in _dpiButtons)
                StyleToggleButton(kvp.Value, kvp.Key == _config.MouseDpi);
            foreach (KeyValuePair<int, Button> kvp in _pollButtons)
                StyleToggleButton(kvp.Value, kvp.Key == _config.MousePollRateHz);
            _chkScrollLed.Checked = _config.MouseScrollLed;
            _chkLogoLed.Checked = _config.MouseLogoLed;
            foreach (Control ctrl in _mouseInnerControls)
                ctrl.Enabled = _config.MouseEnabled;

            _chkKbdEnabled.Checked = _config.KeyboardEnabled;
            _chkKbdStartup.Checked = _config.KeyboardApplyOnStartup;
            foreach (KeyValuePair<KeyboardEffect, Button> kvp in _effectButtons)
                StyleToggleButton(kvp.Value, kvp.Key == _config.KeyboardEffect);
            _colorPreview.BackColor = Color.FromArgb(_config.ColorR, _config.ColorG, _config.ColorB);
            _trkBrightness.Value = Math.Max(0, Math.Min(100, _config.KeyboardBrightness));
            _lblBrightnessVal.Text = $"{_config.KeyboardBrightness}%";
            foreach (Control ctrl in _kbdInnerControls)
                ctrl.Enabled = _config.KeyboardEnabled;
        }
        finally
        {
            _syncingUi = false;
        }
    }

    private void RefreshDevices()
    {
        _devices = HidNative.EnumerateRazerDevices();
        bool hasMouse = RazerProtocol.FindDeathAdder35G(_devices).Count > 0;
        bool hasKbd = RazerProtocol.FindBlackWidow(_devices).Count > 0;

        ReflowVisibleCards(hasMouse, hasKbd);

        if (_devices.Count == 0)
        {
            _lblDevices.Text = "No Razer USB HID devices detected right now.";
            _lblDevices.ForeColor = TextMuted;
            return;
        }

        string distinctPids = string.Join(", ",
            _devices.Select(d => $"{d.ProductName} [0x{d.ProductId:X4}]").Distinct());

        _lblDevices.Text = $"Detected: {distinctPids}";
        _lblDevices.ForeColor = (hasMouse || hasKbd) ? AccentGreen : StatusWarn;
    }

    private void ReflowVisibleCards(bool hasMouse, bool hasKbd)
    {
        const int pad = 20;
        int y = 130;

        _mouseCard.Visible = hasMouse;
        if (hasMouse)
        {
            _mouseCard.Location = SP(pad, y);
            y += 212 + 12;
        }

        _kbdCard.Visible = hasKbd;
        if (hasKbd)
        {
            _kbdCard.Location = SP(pad, y);
            y += 228 + 12;
        }

        bool anyDevice = hasMouse || hasKbd;
        _emptyStateCard.Visible = !anyDevice;
        if (!anyDevice)
        {
            _emptyStateCard.Location = SP(pad, y);
            y += 108 + 12;
        }

        _btnSaveAll.Visible = anyDevice;
        if (anyDevice)
        {
            _btnSaveAll.Location = SP(680 - pad - 185, y);
            _lblStatus.Location = SP(pad, y + 44);
            ClientSize = SS(680, y + 98);
        }
        else
        {
            _lblStatus.Location = SP(pad, y);
            ClientSize = SS(680, y + 54);
        }
    }

    private void PickCustomColor()
    {
        using var dlg = new ColorDialog
        {
            FullOpen = true,
            Color = Color.FromArgb(_config.ColorR, _config.ColorG, _config.ColorB)
        };

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _config.ColorR = dlg.Color.R;
            _config.ColorG = dlg.Color.G;
            _config.ColorB = dlg.Color.B;
            SyncUiFromConfig();
        }
    }

    private void ApplyMouseOnly()
    {
        if (!_config.MouseEnabled)
        {
            SetStatus("Mouse control is disabled.", false);
            return;
        }

        _config.Save();
        RefreshDevices();
        ApplyResult res = RazerProtocol.ApplyDeathAdder35G(
            _devices,
            _config.MouseDpi,
            _config.MousePollRateHz,
            _config.MouseScrollLed,
            _config.MouseLogoLed);

        SetStatus(res.Message, res.Success);
    }

    private void ApplyKeyboardOnly()
    {
        if (!_config.KeyboardEnabled)
        {
            SetStatus("Keyboard control is disabled.", false);
            return;
        }

        _config.Save();
        RefreshDevices();
        ApplyResult res = RazerProtocol.ApplyBlackWidow(
            _devices,
            _config.KeyboardEffect,
            _config.KeyboardBrightness,
            _config.ColorR,
            _config.ColorG,
            _config.ColorB);

        SetStatus(res.Message, res.Success);
    }

    private void ApplyAll()
    {
        _config.Save();
        RefreshDevices();

        bool hasMouse = RazerProtocol.FindDeathAdder35G(_devices).Count > 0;
        bool hasKbd = RazerProtocol.FindBlackWidow(_devices).Count > 0;

        var messages = new List<string>();
        bool anySuccess = false;

        if (hasMouse && _config.MouseEnabled)
        {
            ApplyResult mouseRes = RazerProtocol.ApplyDeathAdder35G(
                _devices,
                _config.MouseDpi,
                _config.MousePollRateHz,
                _config.MouseScrollLed,
                _config.MouseLogoLed);
            messages.Add(mouseRes.Message);
            anySuccess |= mouseRes.Success;
        }

        if (hasKbd && _config.KeyboardEnabled)
        {
            ApplyResult kbdRes = RazerProtocol.ApplyBlackWidow(
                _devices,
                _config.KeyboardEffect,
                _config.KeyboardBrightness,
                _config.ColorR,
                _config.ColorG,
                _config.ColorB);
            messages.Add(kbdRes.Message);
            anySuccess |= kbdRes.Success;
        }

        if (messages.Count == 0)
        {
            SetStatus("No detected devices have control enabled.", false);
            return;
        }

        SetStatus(string.Join("  |  ", messages), anySuccess);
    }

    private void UpdateStartupRegistry(string deviceLabel)
    {
        try
        {
            _config.SyncStartupRegistry();
            bool isEnabled = deviceLabel == "Mouse" ? _config.MouseApplyOnStartup : _config.KeyboardApplyOnStartup;
            SetStatus(
                isEnabled
                    ? $"{deviceLabel} settings will auto-apply on Windows login."
                    : $"{deviceLabel} startup auto-apply disabled.",
                true);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not update startup setting: {ex.Message}", false);
        }
    }

    private void SetStatus(string text, bool success)
    {
        _lblStatus.Text = text;
        _lblStatus.ForeColor = success ? AccentGreen : StatusWarn;
    }

    private static Panel CreateCard(Point location, Size size)
    {
        var panel = new Panel
        {
            Location = location,
            Size = size,
            BackColor = BgCard
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(BorderSubtle);
            e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
        };
        return panel;
    }

    private void AddSectionHeader(Panel card, string title)
    {
        card.Controls.Add(new Label
        {
            Text = title,
            Font = _fontSection,
            ForeColor = TextPrimary,
            AutoSize = false,
            Size = SS(460, 24),
            Location = SP(16, 12),
            TextAlign = ContentAlignment.MiddleLeft
        });
    }

    private Label CreateFieldLabel(string text, Point location) =>
        new()
        {
            Text = text,
            Font = _fontBase,
            ForeColor = TextMuted,
            AutoSize = false,
            Size = SS(104, 24),
            Location = location,
            TextAlign = ContentAlignment.MiddleLeft
        };

    private Button CreateToggleButton(string text, Point location, Size size)
    {
        var btn = new Button
        {
            Text = text,
            Font = _fontToggleNormal,
            Location = location,
            Size = size,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 1;
        return btn;
    }

    private void StyleToggleButton(Button btn, bool selected)
    {
        btn.BackColor = selected ? AccentGreen : BgButton;
        btn.ForeColor = selected ? Color.Black : TextPrimary;
        btn.Font = selected ? _fontToggleSelected : _fontToggleNormal;
        btn.FlatAppearance.BorderColor = selected ? AccentGreen : BorderSubtle;
    }

    private Button CreateActionButton(string text, Point location, Size size, bool secondary)
    {
        var btn = new Button
        {
            Text = text,
            Location = location,
            Size = size,
            FlatStyle = FlatStyle.Flat,
            BackColor = secondary ? BgButton : AccentGreen,
            ForeColor = secondary ? TextPrimary : Color.Black,
            Font = secondary ? _fontBase : _fontActionBold,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = secondary ? BorderSubtle : AccentGreenDark;
        return btn;
    }
}

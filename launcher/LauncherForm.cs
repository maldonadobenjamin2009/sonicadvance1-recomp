using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace SonicAdvanceLauncher;

internal sealed class LauncherForm : Form
{
    private readonly TextBox _romPath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _biosPath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _runnerPath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Label _hash = new() { AutoSize = true, Text = "SHA-1: sin comprobar" };
    private readonly Label _status = new() { AutoSize = true, Text = "Seleccioná tus archivos para comenzar." };
    private readonly CheckBox _fullscreen = new() { Text = "Pantalla completa", AutoSize = true };
    private readonly string _settingsPath = Path.Combine(AppContext.BaseDirectory, "launcher-settings.json");
    private Settings _settings = new();

    public LauncherForm()
    {
        Text = "Sonic Advance 1 Recomp — Launcher";
        Width = 780;
        Height = 390;
        MinimumSize = new Size(680, 350);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(24, 28, 38);
        ForeColor = Color.White;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 3,
            RowCount = 8,
            BackColor = BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        for (int i = 0; i < 8; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        var title = new Label
        {
            Text = "SONIC ADVANCE 1  |  RECOMP",
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            ForeColor = Color.FromArgb(85, 190, 255),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12)
        };
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 3);

        AddRow(layout, 1, "ROM de GBA", _romPath, "Elegir…", ChooseRom);
        AddRow(layout, 2, "BIOS de GBA", _biosPath, "Elegir…", ChooseBios);
        AddRow(layout, 3, "Runner nativo", _runnerPath, "Buscar…", ChooseRunner);

        layout.Controls.Add(_hash, 0, 4);
        layout.SetColumnSpan(_hash, 3);
        _hash.Margin = new Padding(0, 8, 0, 8);

        layout.Controls.Add(_fullscreen, 0, 5);
        layout.SetColumnSpan(_fullscreen, 3);

        var launch = new Button
        {
            Text = "INICIAR SONIC ADVANCE",
            Dock = DockStyle.Fill,
            Height = 42,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 125, 220),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Margin = new Padding(0, 12, 0, 8)
        };
        launch.FlatAppearance.BorderSize = 0;
        launch.Click += (_, _) => LaunchGame();
        layout.Controls.Add(launch, 0, 6);
        layout.SetColumnSpan(launch, 3);

        layout.Controls.Add(_status, 0, 7);
        layout.SetColumnSpan(_status, 3);
        _status.ForeColor = Color.Gainsboro;

        LoadSettings();
        RefreshFields();
        _fullscreen.CheckedChanged += (_, _) =>
        {
            _settings.Fullscreen = _fullscreen.Checked;
            SaveSettings();
        };
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, TextBox box, string buttonText, Action choose)
    {
        var caption = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 8) };
        var button = new Button { Text = buttonText, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(8, 3, 0, 3) };
        button.Click += (_, _) => choose();
        box.Margin = new Padding(0, 4, 0, 4);
        layout.Controls.Add(caption, 0, row);
        layout.Controls.Add(box, 1, row);
        layout.Controls.Add(button, 2, row);
    }

    private void ChooseRom()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Elegí tu ROM de Sonic Advance",
            Filter = "ROM de Game Boy Advance (*.gba)|*.gba|Todos los archivos (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _settings.RomPath = dialog.FileName;
        try
        {
            var hash = Convert.ToHexString(SHA1.HashData(File.ReadAllBytes(dialog.FileName))).ToLowerInvariant();
            _hash.Text = "SHA-1: " + hash;
            _status.Text = string.IsNullOrWhiteSpace(_settings.ExpectedRomSha1)
                ? "ROM seleccionada. La revisión exacta aún no está configurada."
                : hash.Equals(_settings.ExpectedRomSha1, StringComparison.OrdinalIgnoreCase)
                    ? "La ROM coincide con el SHA-1 configurado."
                    : "ATENCIÓN: el SHA-1 no coincide con el configurado.";
        }
        catch (Exception ex) { _status.Text = "No se pudo leer la ROM: " + ex.Message; }
        SaveSettings();
        RefreshFields();
    }

    private void ChooseBios()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Elegí tu BIOS de GBA",
            Filter = "Archivos BIOS (*.bin;*.gba)|*.bin;*.gba|Todos los archivos (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _settings.BiosPath = dialog.FileName;
        SaveSettings();
        RefreshFields();
    }

    private void ChooseRunner()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Elegí el ejecutable nativo de Sonic Advance",
            Filter = "Ejecutable de Windows (*.exe)|*.exe|Todos los archivos (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _settings.RunnerPath = dialog.FileName;
        SaveSettings();
        RefreshFields();
    }

    private void LaunchGame()
    {
        if (!File.Exists(_settings.RomPath))
        {
            MessageBox.Show(this, "Primero elegí una ROM de Sonic Advance que tengas legalmente.", "Falta la ROM", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!File.Exists(_settings.BiosPath))
        {
            MessageBox.Show(this, "Elegí tu archivo BIOS de GBA. El runtime puede exigir una BIOS compatible.", "Falta la BIOS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!File.Exists(_settings.RunnerPath))
        {
            MessageBox.Show(this,
                "Todavía no hay un ejecutable nativo configurado. Este repositorio contiene el launcher, pero el runner de Sonic Advance debe construirse e integrarse con GBARecomp.",
                "Runner no disponible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var start = new ProcessStartInfo(_settings.RunnerPath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(_settings.RunnerPath)!
        };
        start.ArgumentList.Add("--rom");
        start.ArgumentList.Add(_settings.RomPath);
        start.ArgumentList.Add("--bios");
        start.ArgumentList.Add(_settings.BiosPath);
        if (_fullscreen.Checked) start.ArgumentList.Add("--fullscreen");

        try
        {
            Process.Start(start);
            _status.Text = "Se solicitó iniciar el runner nativo.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo iniciar el runner: " + ex.Message, "Error al iniciar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
                _settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(_settingsPath)) ?? new Settings();
        }
        catch { _settings = new Settings(); }
    }

    private void SaveSettings()
    {
        try { File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { _status.Text = "No se pudo guardar la configuración: " + ex.Message; }
    }

    private void RefreshFields()
    {
        _romPath.Text = _settings.RomPath;
        _biosPath.Text = _settings.BiosPath;
        _runnerPath.Text = _settings.RunnerPath;
        _fullscreen.Checked = _settings.Fullscreen;
    }

    private sealed class Settings
    {
        public string RomPath { get; set; } = "";
        public string BiosPath { get; set; } = "";
        public string RunnerPath { get; set; } = "";
        public string ExpectedRomSha1 { get; set; } = "";
        public bool Fullscreen { get; set; }
    }
}

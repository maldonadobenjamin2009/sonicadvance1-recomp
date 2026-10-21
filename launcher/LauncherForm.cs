using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SonicAdvanceLauncher;

/// <summary>
/// Local build front-end. It accepts a user-owned, hash-verified ROM and BIOS,
/// generates the native C/C++ locally, and builds SonicAdvanceRecomp.exe.
/// Nothing is uploaded to GitHub or sent to a remote service.
/// </summary>
internal sealed class LauncherForm : Form
{
    private const string ExpectedRomSha1 = "d842afa7dd1e84de08addb94a51506f1bcafd551";
    private const string ExpectedBiosSha1 = "300c20df6731a33952ded8c436f7f186d25d3492";
    private static readonly string[] ToolchainCandidates =
    {
        @"C:\msys64\mingw64\bin",
        @"C:\msys64\ucrt64\bin"
    };
    private const string MsysUserBin = @"C:\msys64\usr\bin";

    private readonly TextBox _romPath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _biosPath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly TextBox _outputPath = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Label _hash = new() { AutoSize = true, Text = "ROM: todavía no verificada" };
    private readonly Label _status = new() { AutoSize = true, Text = "Elegí tu ROM, la BIOS de GBA y la carpeta de salida." };
    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(14, 17, 24),
        ForeColor = Color.Gainsboro,
        Font = new Font("Consolas", 9F),
        WordWrap = false
    };
    private readonly Button _buildButton = new()
    {
        Text = "GENERAR EXE DEL JUEGO",
        Height = 42,
        Width = 260,
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(0, 125, 220),
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 11F, FontStyle.Bold)
    };
    private readonly CheckBox _launchAfterBuild = new()
    {
        Text = "Abrir el juego al terminar",
        Checked = true,
        AutoSize = true,
        ForeColor = Color.White,
        Margin = new Padding(18, 12, 0, 0)
    };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Style = ProgressBarStyle.Blocks };
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SonicAdvanceRecompBuilder", "builder-settings.json");
    private Settings _settings = new();

    public LauncherForm()
    {
        Text = "Sonic Advance Recomp — Builder";
        Width = 920;
        Height = 650;
        MinimumSize = new Size(800, 570);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(24, 28, 38);
        ForeColor = Color.White;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 3,
            RowCount = 11,
            BackColor = BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        for (int i = 0; i < 10; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        var title = new Label
        {
            Text = "SONIC ADVANCE  |  RECOMP BUILDER",
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            ForeColor = Color.FromArgb(85, 190, 255),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };
        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 3);

        var description = new Label
        {
            Text = "Elegí tu copia local: el programa genera el código nativo y compila SonicAdvanceRecomp.exe en tu PC.",
            AutoSize = true,
            MaximumSize = new Size(830, 0),
            ForeColor = Color.Gainsboro,
            Margin = new Padding(0, 0, 0, 12)
        };
        layout.Controls.Add(description, 0, 1);
        layout.SetColumnSpan(description, 3);

        AddRow(layout, 2, "ROM de Sonic Advance", _romPath, "Elegir…", ChooseRom);
        AddRow(layout, 3, "BIOS de GBA", _biosPath, "Elegir…", ChooseBios);
        AddRow(layout, 4, "Carpeta de salida", _outputPath, "Elegir…", ChooseOutputFolder);

        _hash.Margin = new Padding(0, 8, 0, 6);
        layout.Controls.Add(_hash, 0, 5);
        layout.SetColumnSpan(_hash, 3);

        var privacy = new Label
        {
            Text = "Privacidad: la ROM y la BIOS se leen localmente. No se suben a GitHub ni se incluyen en el ejecutable final.",
            AutoSize = true,
            MaximumSize = new Size(830, 0),
            ForeColor = Color.FromArgb(170, 205, 180),
            Margin = new Padding(0, 0, 0, 8)
        };
        layout.Controls.Add(privacy, 0, 6);
        layout.SetColumnSpan(privacy, 3);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            BackColor = BackColor,
            Margin = new Padding(0, 5, 0, 8)
        };
        _buildButton.FlatAppearance.BorderSize = 0;
        _buildButton.Click += async (_, _) => await BuildGameAsync();
        actions.Controls.Add(_buildButton);
        actions.Controls.Add(_launchAfterBuild);
        layout.Controls.Add(actions, 0, 7);
        layout.SetColumnSpan(actions, 3);

        _progress.Margin = new Padding(0, 2, 0, 8);
        layout.Controls.Add(_progress, 0, 8);
        layout.SetColumnSpan(_progress, 3);

        _status.ForeColor = Color.Gainsboro;
        _status.Margin = new Padding(0, 0, 0, 8);
        layout.Controls.Add(_status, 0, 9);
        layout.SetColumnSpan(_status, 3);

        _log.Margin = new Padding(0, 0, 0, 0);
        layout.Controls.Add(_log, 0, 10);
        layout.SetColumnSpan(_log, 3);

        LoadSettings();
        RefreshFields();
        UpdateRomHashLabel();
        AppendLog("Preparado. Se admite Sonic Advance USA (ASOE, revisión 0) y una BIOS de GBA compatible.");
        AppendLog("Requisito de compilación: MSYS2 MINGW64 o UCRT64 con GCC/G++, CMake, Ninja y SDL2.");
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label,
        TextBox box, string buttonText, Action choose)
    {
        var caption = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 8, 8)
        };
        var button = new Button
        {
            Text = buttonText,
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(8, 3, 0, 3)
        };
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
            Title = "Elegí tu copia de Sonic Advance USA",
            Filter = "ROM de Game Boy Advance (*.gba)|*.gba|Todos los archivos (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _settings.RomPath = dialog.FileName;
        SaveSettings();
        RefreshFields();
        UpdateRomHashLabel();
    }

    private void ChooseBios()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Elegí tu BIOS compatible de Game Boy Advance",
            Filter = "Archivos BIOS (*.bin)|*.bin|Todos los archivos (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _settings.BiosPath = dialog.FileName;
        SaveSettings();
        RefreshFields();
    }

    private void ChooseOutputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Elegí dónde guardar SonicAdvanceRecomp.exe y sus dependencias.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (Directory.Exists(_settings.OutputDirectory))
            dialog.InitialDirectory = _settings.OutputDirectory;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _settings.OutputDirectory = dialog.SelectedPath;
        SaveSettings();
        RefreshFields();
    }

    private void UpdateRomHashLabel()
    {
        if (!File.Exists(_settings.RomPath))
        {
            _hash.Text = "ROM: todavía no seleccionada";
            return;
        }

        try
        {
            using var stream = File.OpenRead(_settings.RomPath);
            var actual = Convert.ToHexString(SHA1.HashData(stream)).ToLowerInvariant();
            _hash.Text = "SHA-1: " + actual;
            _status.Text = actual == ExpectedRomSha1
                ? "La ROM coincide con Sonic Advance USA (ASOE)."
                : "La ROM seleccionada no coincide con la revisión USA admitida.";
        }
        catch (Exception ex)
        {
            _hash.Text = "No se pudo verificar la ROM.";
            _status.Text = ex.Message;
        }
    }

    private async Task BuildGameAsync()
    {
        if (_buildButton.Enabled == false) return;

        try
        {
            if (!File.Exists(_settings.RomPath))
                throw new InvalidOperationException("Primero elegí tu archivo .gba de Sonic Advance.");
            if (!File.Exists(_settings.BiosPath))
                throw new InvalidOperationException("Elegí también una BIOS compatible de GBA; el runtime recompila la BIOS real.");
            if (string.IsNullOrWhiteSpace(_settings.OutputDirectory))
                throw new InvalidOperationException("Elegí la carpeta en la que querés guardar el ejecutable.");
            var toolchainBin = FindToolchainBin();
            AppendLog("Toolchain MSYS2 seleccionado: " + toolchainBin);

            var romHash = GetSha1(_settings.RomPath);
            if (!romHash.Equals(ExpectedRomSha1, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Esta versión solo configura Sonic Advance USA (ASOE, revisión 0). SHA-1 esperado: {ExpectedRomSha1}; recibido: {romHash}. No se ha subido ni copiado la ROM.");
            var biosHash = GetSha1(_settings.BiosPath);
            if (!biosHash.Equals(ExpectedBiosSha1, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"La BIOS no coincide con la revisión compatible. SHA-1 esperado: {ExpectedBiosSha1}; recibido: {biosHash}.");

            var missing = new[] { "gcc.exe", "g++.exe", "cmake.exe", "ninja.exe", "SDL2.dll" }
                .Where(name => !File.Exists(Path.Combine(toolchainBin, name)))
                .ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException(
                    $"Faltan herramientas en {toolchainBin}: {string.Join(", ", missing)}.");

            _buildButton.Enabled = false;
            _progress.Style = ProgressBarStyle.Marquee;
            _status.Text = "Preparando el kit de compilación local…";
            AppendLog("");
            AppendLog("=== Nueva compilación ===");
            AppendLog("La ROM y la BIOS se usarán desde sus rutas locales; no se transmitirán.");
            await Task.Yield();

            var kitRoot = await Task.Run(ExtractEmbeddedBuildKit);
            AppendLog("Kit de compilación: " + kitRoot);

            var script = Path.Combine(kitRoot, "scripts", "build-native.ps1");
            if (!File.Exists(script))
                throw new InvalidOperationException("El kit integrado está incompleto: no se encontró scripts/build-native.ps1.");

            _status.Text = "Recompilando Sonic Advance y enlazando el ejecutable nativo…";
            var exitCode = await RunBuildScriptAsync(kitRoot, script, _settings.RomPath, _settings.BiosPath, toolchainBin);
            if (exitCode != 0)
                throw new InvalidOperationException($"La compilación terminó con código {exitCode}. Revisá el registro de salida de esta ventana.");

            var nativeExe = Path.Combine(kitRoot, "build-native", "SonicAdvanceRecomp.exe");
            if (!File.Exists(nativeExe))
                throw new FileNotFoundException("La compilación terminó pero no produjo SonicAdvanceRecomp.exe.", nativeExe);

            _status.Text = "Empaquetando el ejecutable del juego…";
            var outDir = Path.GetFullPath(_settings.OutputDirectory);
            Directory.CreateDirectory(outDir);
            PackageGame(kitRoot, nativeExe, outDir, _settings.RomPath, _settings.BiosPath, toolchainBin);

            var finalExe = Path.Combine(outDir, "SonicAdvanceRecomp.exe");
            _status.Text = "Ejecutable generado: " + finalExe;
            AppendLog("");
            AppendLog("LISTO: " + finalExe);
            AppendLog("Se copiaron las dependencias del runtime, el launcher integrado y game.toml.");
            AppendLog("La ROM, la BIOS y el código C/C++ generado no se copiaron al paquete.");
            SaveSettings();

            if (_launchAfterBuild.Checked)
            {
                var start = new ProcessStartInfo(finalExe)
                {
                    UseShellExecute = false,
                    WorkingDirectory = outDir
                };
                Process.Start(start);
            }

            MessageBox.Show(this,
                $"Se generó el ejecutable del juego en:\n{finalExe}\n\nEl port todavía es experimental y debe probarse en juego.",
                "Compilación terminada",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "No se pudo completar la compilación.";
            AppendLog("ERROR: " + ex);
            MessageBox.Show(this,
                ex.Message + "\n\nEl registro completo queda en la ventana y se guarda en el perfil local al terminar el proceso.",
                "Error de compilación",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _progress.Style = ProgressBarStyle.Blocks;
            _progress.Value = 0;
            _buildButton.Enabled = true;
        }
    }

    private async Task<int> RunBuildScriptAsync(string kitRoot, string script,
        string romPath, string biosPath, string toolchainBin)
    {
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
            WorkingDirectory = kitRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-NoLogo");
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("-Rom");
        start.ArgumentList.Add(Path.GetFullPath(romPath));
        start.ArgumentList.Add("-Bios");
        start.ArgumentList.Add(Path.GetFullPath(biosPath));

        var embeddedCompilerBin = Path.Combine(kitRoot, "gbarecomp", "build-native");
        start.Environment["Path"] = toolchainBin + ";" + MsysUserBin + ";" +
                                    embeddedCompilerBin + ";" +
                                    (Environment.GetEnvironmentVariable("Path") ?? "");
        using var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) AppendLog(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) AppendLog("[stderr] " + e.Data); };

        AppendLog("Ejecutando scripts/build-native.ps1…");
        AppendLog("PowerShell: " + start.FileName);
        if (!process.Start())
            throw new InvalidOperationException("No se pudo iniciar PowerShell.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        process.WaitForExit(); // Drain asynchronous stdout/stderr callbacks.
        return process.ExitCode;
    }

    private static string FindToolchainBin()
    {
        var required = new[] { "gcc.exe", "g++.exe", "cmake.exe", "ninja.exe", "SDL2.dll" };
        foreach (var candidate in ToolchainCandidates)
        {
            if (required.All(name => File.Exists(Path.Combine(candidate, name))))
                return candidate;
        }

        var diagnostics = ToolchainCandidates.Select(candidate =>
        {
            var missing = required.Where(name => !File.Exists(Path.Combine(candidate, name))).ToArray();
            return $"  {candidate}: " +
                (Directory.Exists(candidate)
                    ? "faltan " + string.Join(", ", missing)
                    : "directorio no encontrado");
        });

        throw new InvalidOperationException(
            "No encontré un toolchain MSYS2 completo. El Builder detecta MINGW64 y UCRT64, " +
            "pero necesita GCC/G++, CMake, Ninja y SDL2.\n\n" +
            string.Join("\n", diagnostics) +
            "\n\nEn la terminal MSYS2 elegida, ejecutá primero pacman -Suy; si te pide cerrar la terminal, reabrila y repetí el comando hasta completar la actualización. Después instalá:\n" +
            "MINGW64: pacman -S --needed mingw-w64-x86_64-toolchain mingw-w64-x86_64-cmake mingw-w64-x86_64-ninja mingw-w64-x86_64-SDL2\n" +
            "UCRT64: pacman -S --needed mingw-w64-ucrt-x86_64-toolchain mingw-w64-ucrt-x86_64-cmake mingw-w64-ucrt-x86_64-ninja mingw-w64-ucrt-x86_64-SDL2");
    }

    private static string ExtractEmbeddedBuildKit()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("buildkit.zip", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            throw new InvalidOperationException(
                "Este ejecutable no incluye el kit de compilación. Descargá SonicAdvanceRecompBuilder.exe desde el artefacto de GitHub Actions; una compilación local del launcher no contiene el kit.");

        using var resource = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("No se pudo abrir el kit de compilación integrado.");
        using var memory = new MemoryStream();
        resource.CopyTo(memory);
        var archiveBytes = memory.ToArray();
        var kitHash = Convert.ToHexString(SHA256.HashData(archiveBytes)).ToLowerInvariant();
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SonicAdvanceRecompBuilder", "buildkit", kitHash);

        if (File.Exists(Path.Combine(root, "scripts", "build-native.ps1")) &&
            File.Exists(Path.Combine(root, "gbarecomp", "build-native", "gba_recompile.exe")))
            return root;

        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        Directory.CreateDirectory(root);

        using var archiveStream = new MemoryStream(archiveBytes, writable: false);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
        var rootWithSeparator = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!target.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("El kit embebido contiene una ruta de archivo no válida.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }

        if (!File.Exists(Path.Combine(root, "scripts", "build-native.ps1")) ||
            !File.Exists(Path.Combine(root, "gbarecomp", "build-native", "gba_recompile.exe")))
            throw new InvalidDataException("El kit extraído no contiene los scripts y la herramienta gba_recompile requeridos.");
        return root;
    }

    private static void PackageGame(string kitRoot, string nativeExe, string outputDir,
        string romPath, string biosPath, string toolchainBin)
    {
        File.Copy(nativeExe, Path.Combine(outputDir, "SonicAdvanceRecomp.exe"), overwrite: true);

        var template = File.ReadAllText(Path.Combine(kitRoot, "game.toml"));
        template = SetSectionPath(template, "bios", biosPath);
        template = SetSectionPath(template, "rom", romPath);
        File.WriteAllText(Path.Combine(outputDir, "game.toml"), template, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var buildAssets = Path.Combine(kitRoot, "build-native", "assets");
        var outAssets = Path.Combine(outputDir, "assets");
        if (Directory.Exists(outAssets)) Directory.Delete(outAssets, recursive: true);
        if (Directory.Exists(buildAssets)) CopyDirectory(buildAssets, outAssets);

        Directory.CreateDirectory(Path.Combine(outputDir, "saves"));

        foreach (var dll in new[]
        {
            "SDL2.dll", "libstdc++-6.dll", "libgcc_s_seh-1.dll",
            "libwinpthread-1.dll", "libssp-0.dll", "libiconv-2.dll",
            "libintl-8.dll", "zlib1.dll"
        })
        {
            var source = Path.Combine(toolchainBin, dll);
            if (File.Exists(source))
                File.Copy(source, Path.Combine(outputDir, dll), overwrite: true);
        }

        if (!File.Exists(Path.Combine(outputDir, "SDL2.dll")))
            throw new FileNotFoundException($"No se encontró SDL2.dll en {toolchainBin} para completar el paquete.");
    }

    private static string SetSectionPath(string toml, string section, string value)
    {
        var lines = toml.Replace("\r\n", "\n").Split('\n');
        var inSection = false;
        var changed = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) &&
                trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                inSection = string.Equals(trimmed[1..^1], section, StringComparison.Ordinal);
                continue;
            }

            if (!inSection || !Regex.IsMatch(lines[i], @"^\s*path\s*="))
                continue;

            var leading = lines[i][..(lines[i].Length - lines[i].TrimStart().Length)];
            lines[i] = leading + "path = " + JsonSerializer.Serialize(Path.GetFullPath(value));
            changed = true;
            break;
        }

        if (!changed)
            throw new InvalidDataException($"No se pudo actualizar [{section}].path en game.toml.");
        return string.Join(Environment.NewLine, lines);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var subdirectory in Directory.GetDirectories(source))
            CopyDirectory(subdirectory, Path.Combine(destination, Path.GetFileName(subdirectory)));
    }

    private static string GetSha1(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA1.HashData(stream)).ToLowerInvariant();
    }

    private void AppendLog(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(new Action<string>(AppendLog), line); }
            catch (InvalidOperationException) { }
            return;
        }

        _log.AppendText(line + Environment.NewLine);
        if (_log.TextLength > 250_000)
            _log.Text = _log.Text[^180_000..];
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
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
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath,
                JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { AppendLog("No se pudo guardar la configuración: " + ex.Message); }
    }

    private void RefreshFields()
    {
        _romPath.Text = _settings.RomPath;
        _biosPath.Text = _settings.BiosPath;
        _outputPath.Text = _settings.OutputDirectory;
    }

    private sealed class Settings
    {
        public string RomPath { get; set; } = "";
        public string BiosPath { get; set; } = "";
        public string OutputDirectory { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SonicAdvanceBuild");
    }
}

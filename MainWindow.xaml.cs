using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace EldenRingLauncher
{
    public class CustomMod
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public bool SyncPassword { get; set; } = false;
    }

    public class LauncherConfig
    {
        public string VanillaExe { get; set; } = "";
        public string CoopExe { get; set; } = "";
        public string ConvergenceExe { get; set; } = "";
        public string OfflineExe { get; set; } = "";
        // Legacy single custom mod (kept for backwards compatibility)
        public string CustomExe { get; set; } = "";
        public string CustomName { get; set; } = "";
        // New: multiple custom mods
        public List<CustomMod> CustomMods { get; set; } = new List<CustomMod>();
        public bool ConvergenceSyncPassword { get; set; } = false;
        public bool AutoClose { get; set; } = true;
        public bool IsMuted { get; set; } = false;
    }

    public partial class MainWindow : Window
    {
        private string _baseDir;
        private string _configPath;
        private string _realVanillaPath;
        private LauncherConfig _config;
        private bool _launched = false;
        private bool _isSeamlessInstalled = false;
        private Action? _dialogYesCallback;
        private System.Windows.Media.MediaPlayer _bgMusic = new System.Windows.Media.MediaPlayer();
        private bool _isMuted = false;
        private string _pendingModPath = "";
        private string _pendingSetupDir = "";

        public MainWindow()
        {
            InitializeComponent();
            
            var versionAttr = System.Reflection.Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .FirstOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute;
            string versionStr = versionAttr?.InformationalVersion ?? "v1.0.0";
            if (versionStr.Contains("+")) versionStr = versionStr.Substring(0, versionStr.IndexOf("+"));
            if (!versionStr.StartsWith("v")) versionStr = "v" + versionStr;
            TxtVersion.Text = "Launcher " + versionStr;

            // FIX: In .NET Single File deployments, AppDomain.BaseDirectory points to the Temp extraction folder.
            // Environment.ProcessPath guarantees we always get the true directory where the .exe sits.
            _baseDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppDomain.CurrentDomain.BaseDirectory;
            _configPath = Path.Combine(_baseDir, "launcher_config.json");
            _realVanillaPath = Path.Combine(_baseDir, "real_start_protected_game.exe");

            if (File.Exists(_configPath))
            {
                try { _config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(_configPath)) ?? new LauncherConfig(); }
                catch { _config = new LauncherConfig(); }
            }
            else { _config = new LauncherConfig(); }

            _config.AutoClose = true; // Always force Auto-Close

            // Migrate legacy single custom mod to the new list
            if (!string.IsNullOrEmpty(_config.CustomExe) && File.Exists(_config.CustomExe))
            {
                string migrateName = string.IsNullOrEmpty(_config.CustomName) ? System.IO.Path.GetFileNameWithoutExtension(_config.CustomExe) : _config.CustomName;
                if (!_config.CustomMods.Any(m => m.Path == _config.CustomExe))
                {
                    _config.CustomMods.Add(new CustomMod { Name = migrateName, Path = _config.CustomExe });
                }
                _config.CustomExe = "";
                _config.CustomName = "";
                SaveConfig();
            }

            CheckSetup();
            StartBackgroundMusic();
        }

        private void StartBackgroundMusic()
        {
            try
            {
                // If in setup mode, default to muted. Otherwise use saved preference.
                _isMuted = SetupOverlay.Visibility == Visibility.Visible ? true : _config.IsMuted;
                _bgMusic.IsMuted = _isMuted;
                BtnMute.Content = _isMuted ? "🔇" : "🔊";
                BtnMute.Foreground = new System.Windows.Media.SolidColorBrush(_isMuted ? System.Windows.Media.Color.FromRgb(150, 150, 150) : System.Windows.Media.Color.FromRgb(200, 162, 60));

                string tempAudioPath = Path.Combine(Path.GetTempPath(), "EldenLauncher_Rites.mp3");
                if (!File.Exists(tempAudioPath))
                {
                    using (Stream? stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("EldenRingAIOLauncher.Rites.mp3") ?? System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("EldenRingLauncher.Rites.mp3"))
                    {
                        if (stream != null)
                        {
                            using (FileStream fileStream = new FileStream(tempAudioPath, FileMode.Create))
                            {
                                stream.CopyTo(fileStream);
                            }
                        }
                    }
                }
                
                if (File.Exists(tempAudioPath))
                {
                    _bgMusic.Open(new Uri(tempAudioPath));
                    _bgMusic.MediaEnded += (s, e) => { _bgMusic.Position = TimeSpan.Zero; _bgMusic.Play(); };
                    _bgMusic.Volume = 0.5; // Half volume so it's not overwhelming
                    _bgMusic.Play();
                }
            }
            catch { /* Ignore audio failures silently */ }
        }

        private void BtnMute_Click(object sender, RoutedEventArgs e)
        {
            _isMuted = !_isMuted;
            _bgMusic.IsMuted = _isMuted;
            _config.IsMuted = _isMuted;
            SaveConfig();

            BtnMute.Content = _isMuted ? "🔇" : "🔊";
            BtnMute.Foreground = new System.Windows.Media.SolidColorBrush(_isMuted ? System.Windows.Media.Color.FromRgb(150, 150, 150) : System.Windows.Media.Color.FromRgb(200, 162, 60));
        }

        private void Endorse_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = "https://www.nexusmods.com/eldenring/mods/10293", UseShellExecute = true });
            e.Handled = true;
        }

        private void ShowModernDialog(string title, string message, bool isYesNo = false, Action? onYes = null)
        {
            MainUI.Visibility = Visibility.Collapsed;
            SetupOverlay.Visibility = Visibility.Collapsed;
            DialogOverlay.Visibility = Visibility.Visible;
            
            DialogTitle.Text = title;
            DialogMessage.Text = message;
            
            if (isYesNo)
            {
                DialogBtnNo.Visibility = Visibility.Visible;
                DialogBtnYes.Content = "Yes";
            }
            else
            {
                DialogBtnNo.Visibility = Visibility.Collapsed;
                DialogBtnYes.Content = "OK";
            }
            
            _dialogYesCallback = onYes;
        }

        private void DialogBtnYes_Click(object sender, RoutedEventArgs e)
        {
            DialogOverlay.Visibility = Visibility.Collapsed;
            MainUI.Visibility = Visibility.Visible;
            _dialogYesCallback?.Invoke();
            _dialogYesCallback = null;
        }

        private void DialogBtnNo_Click(object sender, RoutedEventArgs e)
        {
            DialogOverlay.Visibility = Visibility.Collapsed;
            MainUI.Visibility = Visibility.Visible;
            _dialogYesCallback = null;
        }

        private void LoadConfig()
        {
            if (File.Exists(_configPath))
            {
                try { _config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(_configPath)) ?? new LauncherConfig(); }
                catch { _config = new LauncherConfig(); }
            }
            else { _config = new LauncherConfig(); }
        }

        private void SaveConfig()
        {
            File.WriteAllText(_configPath, JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void UpdateSeamlessPasswordUI(bool isInstalled)
        {
            _isSeamlessInstalled = isInstalled;
            ChkConvergenceSync.Visibility = isInstalled ? Visibility.Visible : Visibility.Collapsed;
            if (_config != null) ChkConvergenceSync.IsChecked = _config.ConvergenceSyncPassword;

            if (!isInstalled)
            {
                SeamlessPasswordPanel.Visibility = Visibility.Collapsed;
                RenderCustomMods();
                return;
            }

            string iniPath = Path.Combine(_baseDir, "SeamlessCoop", "ersc_settings.ini");
            if (!File.Exists(iniPath) && !string.IsNullOrEmpty(_config.CoopExe))
            {
                iniPath = Path.Combine(Path.GetDirectoryName(_config.CoopExe), "ersc_settings.ini");
            }

            if (File.Exists(iniPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(iniPath);
                    foreach (string line in lines)
                    {
                        if (line.Trim().StartsWith("cooppassword"))
                        {
                            var parts = line.Split('=');
                            if (parts.Length > 1)
                            {
                                TxtSeamlessPassword.Text = parts[1].Trim();
                            }
                            break;
                        }
                    }
                    SeamlessPasswordPanel.Visibility = Visibility.Visible;
                }
                catch { }
            }
            else
            {
                SeamlessPasswordPanel.Visibility = Visibility.Collapsed;
            }
            
            RenderCustomMods();
        }

        private void SyncPasswordToIni(string targetExePath)
        {
            if (string.IsNullOrEmpty(targetExePath)) return;
            string targetIni = Path.Combine(Path.GetDirectoryName(targetExePath) ?? "", "ersc_settings.ini");
            if (!File.Exists(targetIni)) return;

            try
            {
                string[] lines = File.ReadAllLines(targetIni);
                bool replaced = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Trim().StartsWith("cooppassword"))
                    {
                        lines[i] = "cooppassword = " + TxtSeamlessPassword.Text;
                        replaced = true;
                        break;
                    }
                }
                if (replaced) File.WriteAllLines(targetIni, lines);
            }
            catch { }
        }

        private void CheckSetup()
        {
            string exeName = Path.GetFileName(Environment.ProcessPath ?? "").ToLower();
            if (exeName != "start_protected_game.exe")
            {
                MainUI.Visibility = Visibility.Collapsed;
                SetupOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                MainUI.Visibility = Visibility.Visible;
                SetupOverlay.Visibility = Visibility.Collapsed;
                CheckModStatuses();
            }
        }

        private void CheckModStatuses()
        {
            // Offline
            if (string.IsNullOrEmpty(_config.OfflineExe) || !File.Exists(_config.OfflineExe))
            {
                var files = Directory.GetFiles(_baseDir, "EldenRingOfflineLauncher-*.exe");
                bool found = files.Length > 0;
                TxtOfflineStatus.Text = found ? "Ready" : "Click to locate";
                BtnDownloadOffline.Visibility = found ? Visibility.Collapsed : Visibility.Visible;
            }
            else 
            {
                TxtOfflineStatus.Text = "Ready";
                BtnDownloadOffline.Visibility = Visibility.Collapsed;
            }

            // Seamless
            string def1 = Path.Combine(_baseDir, "ersc_launcher.exe");
            string def2 = Path.Combine(_baseDir, "SeamlessCoop", "ersc_launcher.exe");
            if (string.IsNullOrEmpty(_config.CoopExe) || !File.Exists(_config.CoopExe))
            {
                bool found = File.Exists(def1) || File.Exists(def2);
                TxtCoopStatus.Text = found ? "Ready" : "Click to locate";
                BtnDownloadCoop.Visibility = found ? Visibility.Collapsed : Visibility.Visible;
                UpdateSeamlessPasswordUI(found);
            }
            else 
            {
                TxtCoopStatus.Text = "Ready";
                BtnDownloadCoop.Visibility = Visibility.Collapsed;
                UpdateSeamlessPasswordUI(true);
            }

            // Convergence
            string def3 = Path.Combine(_baseDir, "Start_Convergence.bat");
            if (string.IsNullOrEmpty(_config.ConvergenceExe) || !File.Exists(_config.ConvergenceExe))
            {
                if (File.Exists(def3))
                {
                    TxtConvergenceStatus.Text = "Ready";
                    CardConvergence.Visibility = Visibility.Visible;
                }
                else
                {
                    CardConvergence.Visibility = Visibility.Collapsed;
                }
            }
            else 
            {
                TxtConvergenceStatus.Text = "Ready";
                CardConvergence.Visibility = Visibility.Visible;
            }

            // Custom Mods (dynamic)
            RenderCustomMods();
        }

        private void RenderCustomMods()
        {
            CustomModsContainer.Children.Clear();

            foreach (var mod in _config.CustomMods)
            {
                if (!File.Exists(mod.Path)) continue;

                // Separator
                var sep = new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)), Margin = new Thickness(10, 10, 10, 10) };
                CustomModsContainer.Children.Add(sep);

                // Wrapper to hold the card and the floating delete button
                var wrapperGrid = new Grid();

                // Card button (Main Launch)
                var btn = new Button();
                btn.Style = (Style)FindResource("CardButton");
                var capturedMod = mod;
                btn.Click += (s, e) => 
                { 
                    if (File.Exists(capturedMod.Path)) 
                    {
                        if (capturedMod.SyncPassword) SyncPasswordToIni(capturedMod.Path);
                        LaunchMod(capturedMod.Path); 
                    }
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var accent = new Border { Background = new SolidColorBrush(Color.FromRgb(0x2e, 0xa0, 0x43)), Width = 3, Height = 48, CornerRadius = new CornerRadius(2), Margin = new Thickness(12, 0, 12, 0) };
                Grid.SetColumn(accent, 0);
                grid.Children.Add(accent);

                var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Orientation = Orientation.Horizontal };
                Grid.SetColumn(sp, 1);
                var icon = new TextBlock { Text = "\u25B6 ", FontSize = 20, Foreground = new SolidColorBrush(Color.FromRgb(0x2e, 0xa0, 0x43)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
                sp.Children.Add(icon);
                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var title = new TextBlock { Text = mod.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)) };
                var status = new TextBlock { Text = "Ready", FontSize = 10, Foreground = (Brush)FindResource("TextSubBrush") };
                textStack.Children.Add(title);
                textStack.Children.Add(status);
                sp.Children.Add(textStack);
                grid.Children.Add(sp);

                if (_isSeamlessInstalled)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var chk = new CheckBox();
                    chk.Content = "Sync Seamless Password";
                    chk.Foreground = (Brush)FindResource("TextSubBrush");
                    chk.FontSize = 10;
                    chk.VerticalAlignment = VerticalAlignment.Center;
                    chk.Margin = new Thickness(0, 0, 15, 0);
                    chk.IsChecked = capturedMod.SyncPassword;
                    Grid.SetColumn(chk, 2);
                    grid.Children.Add(chk);

                    chk.PreviewMouseLeftButtonDown += (s, e) =>
                    {
                        chk.IsChecked = !chk.IsChecked;
                        capturedMod.SyncPassword = chk.IsChecked ?? false;
                        SaveConfig();
                        e.Handled = true;
                    };
                }

                btn.Content = grid;
                wrapperGrid.Children.Add(btn);

                // Dedicated Delete Button (Floating top right)
                var deleteBtn = new TextBlock();
                deleteBtn.Text = "✕";
                deleteBtn.FontSize = 14;
                deleteBtn.FontWeight = FontWeights.Bold;
                deleteBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7F, 0x6A));
                deleteBtn.VerticalAlignment = VerticalAlignment.Top;
                deleteBtn.HorizontalAlignment = HorizontalAlignment.Right;
                deleteBtn.Margin = new Thickness(0, 8, 8, 0);
                deleteBtn.Cursor = Cursors.Hand;
                deleteBtn.ToolTip = "Remove custom mod";
                deleteBtn.MouseEnter += (s, e) => { deleteBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xcc, 0x44, 0x44)); };
                deleteBtn.MouseLeave += (s, e) => { deleteBtn.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7F, 0x6A)); };
                deleteBtn.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    _config.CustomMods.Remove(capturedMod);
                    SaveConfig();
                    RenderCustomMods();
                    e.Handled = true;
                };
                wrapperGrid.Children.Add(deleteBtn);

                CustomModsContainer.Children.Add(wrapperGrid);
            }
        }

        private void BtnSetupBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "Select start_protected_game.exe",
                Filter = "start_protected_game.exe|start_protected_game.exe"
            };

            if (dlg.ShowDialog() == true)
            {
                string fp = dlg.FileName;
                string targetDir = Path.GetDirectoryName(fp) ?? "";
                string backup = Path.Combine(targetDir, "real_start_protected_game.exe");
                string uninstaller = Path.Combine(targetDir, "Uninstall_Launcher.bat");

                if (File.Exists(backup) || File.Exists(uninstaller))
                {
                    _pendingSetupDir = targetDir;
                    SetupOverlay.Visibility = Visibility.Collapsed;
                    AlreadyInstalledOverlay.Visibility = Visibility.Visible;
                    return;
                }

                File.Move(fp, backup);
                File.Copy(Environment.ProcessPath ?? "", Path.Combine(targetDir, "start_protected_game.exe"), true);

                string batCode = "@echo off\n" +
                                 "echo Uninstalling Elden Ring AIO Launcher...\n" +
                                 "if exist \"start_protected_game.exe\" (\n" +
                                 "    del /f /q \"start_protected_game.exe\"\n" +
                                 "    if exist \"real_start_protected_game.exe\" (\n" +
                                 "        ren \"real_start_protected_game.exe\" \"start_protected_game.exe\"\n" +
                                 "    )\n" +
                                 ")\n" +
                                 "del /f /q \"launcher_config.json\" \"launcher_crash.log\" \"launcher_error.log\" \"launcher_debug.log\"\n" +
                                 "echo Launcher successfully uninstalled. Game restored to Vanilla.\n" +
                                 "pause\n" +
                                 "start /b \"\" cmd /c del \"%~f0\"&exit /b\n";
                File.WriteAllText(uninstaller, batCode);
                System.Media.SystemSounds.Asterisk.Play();
                ShowModernDialog("Setup Complete", "Please close this window now and launch the game through steam.", false, () => Application.Current.Shutdown());
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private string PromptForMod(string modName)
        {
            OpenFileDialog dlg = new OpenFileDialog { Title = $"Select {modName} Executable", Filter = "Executables (*.exe;*.bat;*.me3)|*.exe;*.bat;*.me3" };
            return dlg.ShowDialog() == true ? dlg.FileName : "";
        }

        private void LaunchMod(string path)
        {
            if (_launched) return;
            if (File.Exists(path))
            {
                _launched = true;
                
                // 1. We must completely close start_protected_game.exe instantly, or Seamless Co-op aborts.
                // 2. We must route through explorer.exe to completely escape Steam's Job Object constraint.
                // 3. We must enforce the CWD, because explorer.exe strips it.
                // Fix: We write a temporary batch script that handles the CWD and delays the launch, 
                // run IT through explorer, and instantly kill our C# process. No VBScripts needed (no virus flags).
                
                string batPath = Path.Combine(Path.GetTempPath(), "launch_mod_delayed.bat");
                string targetDir = Path.GetDirectoryName(path) ?? _baseDir;
                
                string batCode = "@echo off\n" +
                                 "ping 127.0.0.1 -n 3 > nul\n" + // Wait 2 seconds for launcher to fully close
                                 $"cd /d \"{targetDir}\"\n" +
                                 $"start \"\" \"{path}\"\n" +
                                 "start /b \"\" cmd /c del \"%~f0\"&exit /b\n"; // Self-delete
                File.WriteAllText(batPath, batCode);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{batPath}\"",
                    UseShellExecute = false
                });

                if (_config.AutoClose)
                {
                    // Instant kill to ensure Seamless Co-op doesn't see us in memory!
                    Environment.Exit(0); 
                }
            }
        }

        private void BtnVanilla_Click(object sender, RoutedEventArgs e)
        {
            if (_launched) return;
            if (File.Exists(_realVanillaPath))
            {
                _launched = true;
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = _realVanillaPath,
                    WorkingDirectory = _baseDir,
                    UseShellExecute = false
                };
                psi.EnvironmentVariables["SteamAppId"] = "1245620";
                Process.Start(psi);
                if (_config.AutoClose) Application.Current.Shutdown();
            }
            else ShowModernDialog("Error", "Could not find real_start_protected_game.exe");
        }

        private void BtnOffline_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_config.OfflineExe) || !File.Exists(_config.OfflineExe))
            {
                var files = Directory.GetFiles(_baseDir, "EldenRingOfflineLauncher-*.exe");
                if (files.Length > 0)
                {
                    Array.Sort(files);
                    _config.OfflineExe = files[files.Length - 1]; // Get latest version
                    SaveConfig();
                }
                else
                {
                    _config.OfflineExe = PromptForMod("Offline Mode Launcher");
                    SaveConfig();
                }
            }
            if (!string.IsNullOrEmpty(_config.OfflineExe) && File.Exists(_config.OfflineExe)) LaunchMod(_config.OfflineExe);
        }

        private void BtnDownloadOffline_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = "https://www.nexusmods.com/eldenring/mods/98", UseShellExecute = true });
            e.Handled = true;
        }

        private void BtnDownloadCoop_Click(object sender, MouseButtonEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = "https://www.nexusmods.com/eldenring/mods/510", UseShellExecute = true });
            e.Handled = true;
        }

        private void TxtSeamlessPassword_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            TxtSeamlessPassword.Focus();
            e.Handled = true;
        }

        private void ChkConvergenceSync_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (_config != null && sender is CheckBox chk)
            {
                _config.ConvergenceSyncPassword = chk.IsChecked ?? false;
                SaveConfig();
            }
        }

        private void ChkSync_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is CheckBox chk)
            {
                chk.IsChecked = !chk.IsChecked;
                e.Handled = true;
            }
        }

        private void BtnCoop_Click(object sender, RoutedEventArgs e)
        {
            string defaultPath1 = Path.Combine(_baseDir, "ersc_launcher.exe");
            string defaultPath2 = Path.Combine(_baseDir, "SeamlessCoop", "ersc_launcher.exe");
            
            if (string.IsNullOrEmpty(_config.CoopExe) || !File.Exists(_config.CoopExe))
            {
                if (File.Exists(defaultPath1)) {
                    _config.CoopExe = defaultPath1;
                    SaveConfig();
                } else if (File.Exists(defaultPath2)) {
                    _config.CoopExe = defaultPath2;
                    SaveConfig();
                } else {
                    _config.CoopExe = PromptForMod("Seamless Co-op");
                    SaveConfig();
                }
            }
            if (!string.IsNullOrEmpty(_config.CoopExe) && File.Exists(_config.CoopExe)) 
            {
                string iniPath = Path.Combine(Path.GetDirectoryName(_config.CoopExe), "ersc_settings.ini");
                if (!File.Exists(iniPath))
                    iniPath = Path.Combine(_baseDir, "SeamlessCoop", "ersc_settings.ini");
                
                if (File.Exists(iniPath))
                {
                    try
                    {
                        string[] lines = File.ReadAllLines(iniPath);
                        bool replaced = false;
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i].Trim().StartsWith("cooppassword"))
                            {
                                lines[i] = "cooppassword = " + TxtSeamlessPassword.Text;
                                replaced = true;
                                break;
                            }
                        }
                        if (replaced) File.WriteAllLines(iniPath, lines);
                    }
                    catch { }
                }

                LaunchMod(_config.CoopExe);
            }
        }

        private void BtnConvergence_Click(object sender, RoutedEventArgs e)
        {
            string defaultPath = Path.Combine(_baseDir, "Start_Convergence.bat");
            if (string.IsNullOrEmpty(_config.ConvergenceExe) || !File.Exists(_config.ConvergenceExe))
            {
                if (File.Exists(defaultPath)) {
                    _config.ConvergenceExe = defaultPath;
                    SaveConfig();
                } else {
                    _config.ConvergenceExe = PromptForMod("The Convergence");
                    SaveConfig();
                }
            }
            if (!string.IsNullOrEmpty(_config.ConvergenceExe) && File.Exists(_config.ConvergenceExe)) 
            {
                if (_config.ConvergenceSyncPassword) SyncPasswordToIni(_config.ConvergenceExe);
                LaunchMod(_config.ConvergenceExe);
            }
        }

        private void BtnAddCustomMod_Click(object sender, RoutedEventArgs e)
        {
            string path = PromptForMod("Custom Mod");
            if (!string.IsNullOrEmpty(path))
            {
                _pendingModPath = path;
                TxtNameModInput.Text = System.IO.Path.GetFileNameWithoutExtension(path);
                TxtNameModInput.SelectAll();
                TxtNameModInput.Focus();
                NameModOverlay.Visibility = Visibility.Visible;
                MainUI.Visibility = Visibility.Collapsed;
            }
            e.Handled = true;
        }

        private void NameModConfirm_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtNameModInput.Text.Trim();
            if (string.IsNullOrEmpty(name)) name = System.IO.Path.GetFileNameWithoutExtension(_pendingModPath);
            _config.CustomMods.Add(new CustomMod { Name = name, Path = _pendingModPath });
            _pendingModPath = "";
            SaveConfig();
            NameModOverlay.Visibility = Visibility.Collapsed;
            MainUI.Visibility = Visibility.Visible;
            RenderCustomMods();
        }

        private void NameModCancel_Click(object sender, RoutedEventArgs e)
        {
            _pendingModPath = "";
            NameModOverlay.Visibility = Visibility.Collapsed;
            MainUI.Visibility = Visibility.Visible;
        }

        private void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            ShowModernDialog("Uninstall Launcher", "Are you sure you want to completely uninstall the launcher and restore the vanilla game files?", true, () => 
            {
                string tmp = Path.Combine(Path.GetTempPath(), "cleanup_launcher_internal.bat");
                string batCode = "@echo off\n" +
                                 "ping 127.0.0.1 -n 3 > nul\n" +
                                 "if exist \"start_protected_game.exe\" (\n" +
                                 "    del /f /q \"start_protected_game.exe\"\n" +
                                 "    if exist \"real_start_protected_game.exe\" (\n" +
                                 "        ren \"real_start_protected_game.exe\" \"start_protected_game.exe\"\n" +
                                 "    )\n" +
                                 ")\n" +
                                 $"del /f /q \"{tmp}\" \"launcher_config.json\" \"Uninstall_Launcher.bat\" \"launcher_crash.log\" \"launcher_error.log\" \"launcher_debug.log\"\n" +
                                 "start /b \"\" cmd /c del \"%~f0\"&exit /b\n";
                File.WriteAllText(tmp, batCode);
                Process.Start(new ProcessStartInfo { FileName = tmp, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                System.Media.SystemSounds.Asterisk.Play();
                System.Threading.Thread.Sleep(300); // Give the system sound time to play before the process dies
                Application.Current.Shutdown();
            });
        }

        private void BtnReinstall_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pendingSetupDir)) return;

            string targetExe = Path.Combine(_pendingSetupDir, "start_protected_game.exe");
            string backup = Path.Combine(_pendingSetupDir, "real_start_protected_game.exe");
            string uninstaller = Path.Combine(_pendingSetupDir, "Uninstall_Launcher.bat");

            // Step 1: Uninstall — same logic as Uninstall_Launcher.bat
            if (File.Exists(targetExe)) File.Delete(targetExe);
            if (File.Exists(backup)) File.Move(backup, targetExe);
            foreach (string f in new[] {
                Path.Combine(_pendingSetupDir, "launcher_config.json"),
                Path.Combine(_pendingSetupDir, "launcher_crash.log"),
                Path.Combine(_pendingSetupDir, "launcher_error.log"),
                Path.Combine(_pendingSetupDir, "launcher_debug.log") })
            {
                if (File.Exists(f)) File.Delete(f);
            }
            if (File.Exists(uninstaller)) File.Delete(uninstaller);

            // Step 2: Clean install — same logic as BtnSetupBrowse_Click
            backup = Path.Combine(_pendingSetupDir, "real_start_protected_game.exe");
            File.Move(targetExe, backup);
            File.Copy(Environment.ProcessPath ?? "", targetExe, true);

            string batCode = "@echo off\n" +
                             "echo Uninstalling Elden Ring AIO Launcher...\n" +
                             "if exist \"start_protected_game.exe\" (\n" +
                             "    del /f /q \"start_protected_game.exe\"\n" +
                             "    if exist \"real_start_protected_game.exe\" (\n" +
                             "        ren \"real_start_protected_game.exe\" \"start_protected_game.exe\"\n" +
                             "    )\n" +
                             ")\n" +
                             "del /f /q \"launcher_config.json\" \"launcher_crash.log\" \"launcher_error.log\" \"launcher_debug.log\"\n" +
                             "echo Launcher successfully uninstalled. Game restored to Vanilla.\n" +
                             "pause\n" +
                             "start /b \"\" cmd /c del \"%~f0\"&exit /b\n";
            File.WriteAllText(uninstaller, batCode);

            AlreadyInstalledOverlay.Visibility = Visibility.Collapsed;
            System.Media.SystemSounds.Asterisk.Play();
            ShowModernDialog("Reinstall Complete", "The launcher has been reinstalled successfully.\n\nPlease close this window and launch the game through Steam.", false, () => Application.Current.Shutdown());
        }

        private void BtnUninstallFromSetup_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pendingSetupDir)) return;

            string uninstaller = Path.Combine(_pendingSetupDir, "Uninstall_Launcher.bat");
            if (File.Exists(uninstaller))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uninstaller,
                    WorkingDirectory = _pendingSetupDir,
                    UseShellExecute = true
                });
            }
            Application.Current.Shutdown();
        }

        private void BtnAlreadyInstalledClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
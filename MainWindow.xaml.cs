using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace EldenRingLauncher
{
    public class LauncherConfig
    {
        public string VanillaExe { get; set; } = "";
        public string CoopExe { get; set; } = "";
        public string ConvergenceExe { get; set; } = "";
        public string OfflineExe { get; set; } = "";
        public string CustomExe { get; set; } = "";
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
        private Action? _dialogYesCallback;
        private System.Windows.Media.MediaPlayer _bgMusic = new System.Windows.Media.MediaPlayer();
        private bool _isMuted = false;

        public MainWindow()
        {
            InitializeComponent();
            
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
            }
            else 
            {
                TxtCoopStatus.Text = "Ready";
                BtnDownloadCoop.Visibility = Visibility.Collapsed;
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

            // Custom Mod
            if (!string.IsNullOrEmpty(_config.CustomExe) && File.Exists(_config.CustomExe))
            {
                TxtCustomModTitle.Text = Path.GetFileName(_config.CustomExe);
                TxtCustomModStatus.Text = "Ready";
                BtnDeleteCustomMod.Visibility = Visibility.Visible;
                CardCustomMod.Visibility = Visibility.Visible;
            }
            else
            {
                CardCustomMod.Visibility = Visibility.Collapsed;
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
                    ShowModernDialog("Already Installed", "The Elden Ring AIO Launcher is already installed in this directory!\n\nPlease launch the game directly through Steam to use the launcher.\n\nInstallation aborted to prevent overwriting your original game files.", false, () => Application.Current.Shutdown());
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
            if (!string.IsNullOrEmpty(_config.CoopExe) && File.Exists(_config.CoopExe)) LaunchMod(_config.CoopExe);
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
            if (!string.IsNullOrEmpty(_config.ConvergenceExe) && File.Exists(_config.ConvergenceExe)) LaunchMod(_config.ConvergenceExe);
        }

        private void BtnAddCustomMod_Click(object sender, MouseButtonEventArgs e)
        {
            string path = PromptForMod("Custom Mod");
            if (!string.IsNullOrEmpty(path))
            {
                _config.CustomExe = path;
                SaveConfig();
                CheckModStatuses();
            }
            e.Handled = true;
        }

        private void BtnLaunchCustomMod_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_config.CustomExe) && File.Exists(_config.CustomExe)) LaunchMod(_config.CustomExe);
        }

        private void BtnDeleteCustomMod_Click(object sender, MouseButtonEventArgs e)
        {
            _config.CustomExe = "";
            SaveConfig();
            CheckModStatuses();
            e.Handled = true;
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
    }
}
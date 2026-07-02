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
    }

    public partial class MainWindow : Window
    {
        private string _baseDir;
        private string _configPath;
        private string _realVanillaPath;
        private LauncherConfig _config;
        private bool _launched = false;
        private Action _dialogYesCallback;

        public MainWindow()
        {
            InitializeComponent();
            
            string exePath = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory;
            _baseDir = Path.GetDirectoryName(exePath) ?? "";
            _configPath = Path.Combine(_baseDir, "launcher_config.json");
            _realVanillaPath = Path.Combine(_baseDir, "real_start_protected_game.exe");
            
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                File.WriteAllText(Path.Combine(_baseDir, "launcher_crash.log"), e.ExceptionObject.ToString());
            };
            
            LoadConfig();
            ChkAutoClose.IsChecked = _config.AutoClose;
            CheckSetup();
        }

        private void ShowModernDialog(string title, string message, bool isYesNo = false, Action onYes = null)
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
                                 "del /f /q \"launcher_config.json\"\n" +
                                 "echo Launcher successfully uninstalled. Game restored to Vanilla.\n" +
                                 "pause\n" +
                                 "start /b \"\" cmd /c del \"%~f0\"&exit /b\n";
                File.WriteAllText(uninstaller, batCode);

                ShowModernDialog("Setup Complete", "Please close this window and launch the game directly through Steam from now on so Steam tracks your hours.", false, () => Application.Current.Shutdown());
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void ChkAutoClose_Changed(object sender, RoutedEventArgs e)
        {
            if (_config != null)
            {
                _config.AutoClose = ChkAutoClose.IsChecked ?? true;
                SaveConfig();
            }
        }

        private string PromptForMod(string modName)
        {
            OpenFileDialog dlg = new OpenFileDialog { Title = $"Select {modName} Executable", Filter = "Executables (*.exe;*.bat)|*.exe;*.bat" };
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

        private void BtnAddMod_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_config.CustomExe) || !File.Exists(_config.CustomExe))
            {
                _config.CustomExe = PromptForMod("Custom Mod");
                SaveConfig();
            }
            if (!string.IsNullOrEmpty(_config.CustomExe) && File.Exists(_config.CustomExe)) LaunchMod(_config.CustomExe);
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
                                 $"del /f /q \"{tmp}\" launcher_config.json Uninstall_Launcher.bat\n" +
                                 "start /b \"\" cmd /c del \"%~f0\"&exit /b\n";
                File.WriteAllText(tmp, batCode);
                Process.Start(new ProcessStartInfo { FileName = tmp, UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
                Application.Current.Shutdown();
            });
        }
    }
}
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ZapretDashboard.Services
{
    public class ZapretService
    {
        public string WorkingDirectory { get; }
        private Process? _currentProcess;

        public ZapretService()
        {
            WorkingDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ZapretDashboard", "zapret");

            ExtractResourcesIfNeeded();
        }

        private void ExtractResourcesIfNeeded()
        {
            Stop(); // Убиваем процессы, чтобы освободить файлы

            if (Directory.Exists(WorkingDirectory))
            {
                try { Directory.Delete(WorkingDirectory, true); } catch { }
            }

            if (!Directory.Exists(WorkingDirectory))
                Directory.CreateDirectory(WorkingDirectory);

            var asm = Assembly.GetExecutingAssembly();
            foreach (var resName in asm.GetManifestResourceNames())
            {
                if (!resName.StartsWith("ZapretDashboard.Resources.zapret.", StringComparison.OrdinalIgnoreCase))
                    continue;

                string relativeName = resName.Substring("ZapretDashboard.Resources.zapret.".Length);

                if (relativeName.StartsWith("bin.", StringComparison.OrdinalIgnoreCase))
                    relativeName = relativeName.Replace("bin.", "bin\\");
                else if (relativeName.StartsWith("lists.", StringComparison.OrdinalIgnoreCase))
                    relativeName = relativeName.Replace("lists.", "lists\\");
                else if (relativeName.StartsWith("utils.", StringComparison.OrdinalIgnoreCase))
                    relativeName = relativeName.Replace("utils.", "utils\\");
                else if (relativeName.StartsWith("windivert64.", StringComparison.OrdinalIgnoreCase))
                    relativeName = relativeName.Replace("windivert64.", "windivert64\\");

                if (relativeName.Contains("\\"))
                {
                    int lastDot = relativeName.LastIndexOf('.');
                    if (lastDot != -1)
                    {
                        string pathPart = relativeName.Substring(0, lastDot).Replace('.', '\\');
                        string extPart = relativeName.Substring(lastDot);
                        relativeName = pathPart + extPart;
                    }
                }

                string targetPath = Path.Combine(WorkingDirectory, relativeName);
                string? dirName = Path.GetDirectoryName(targetPath);
                if (dirName != null) Directory.CreateDirectory(dirName);

                try
                {
                    using var stream = asm.GetManifestResourceStream(resName);
                    if (stream != null)
                    {
                        using var file = File.Create(targetPath);
                        stream.CopyTo(file);
                    }
                }
                catch (IOException) { } // Пропускаем заблокированные файлы (.sys)
            }
        }

        public string[] GetStrategyFiles()
        {
            if (!Directory.Exists(WorkingDirectory)) return Array.Empty<string>();
            var allBats = Directory.GetFiles(WorkingDirectory, "*.bat", SearchOption.AllDirectories);
            return allBats.Where(f => !Path.GetFileName(f).ToLower().Contains("service")).OrderBy(f => f).ToArray();
        }

        public Task<bool> StartAsync(string batPath)
        {
            return Task.Run(() =>
            {
                try
                {
                    Stop();
                    Thread.Sleep(1000);

                    string batDir = Path.GetDirectoryName(batPath) ?? WorkingDirectory;

                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{batPath}\"",
                        WorkingDirectory = batDir,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        Verb = "runas" // UAC
                    };

                    _currentProcess = Process.Start(psi);

                    // Ждем, чтобы winws.exe успел инициализироваться
                    Thread.Sleep(1500);

                    return _currentProcess != null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка запуска: {ex.Message}");
                    return false;
                }
            });
        }

        public void Stop()
        {
            try
            {
                if (_currentProcess != null && !_currentProcess.HasExited)
                {
                    _currentProcess.Kill(true);
                }
            }
            catch { }

            for (int i = 0; i < 3; i++)
            {
                var processes = Process.GetProcessesByName("winws");
                if (processes.Length == 0) break;

                foreach (var p in processes)
                {
                    try { p.Kill(); p.WaitForExit(1000); } catch { }
                }
                Thread.Sleep(200);
            }
            _currentProcess = null;
        }
    }
}
using System;
using System.Windows.Forms;
using SQLitePCL;

namespace BATIR
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Batteries_V2.Init();
            var selfTestMode = args.Any(a => a.StartsWith("--self-test", StringComparison.OrdinalIgnoreCase));
            if (selfTestMode)
            {
                var testRoot = Path.Combine(Path.GetTempPath(), "BATIR-SelfTest-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(testRoot);
                Environment.SetEnvironmentVariable("BATIR_DATABASE_PATH", Path.Combine(testRoot, "batir.db"));
            }
            if (args.Any(a => string.Equals(a, "--self-test-stage2", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = Stage2SelfTest.Run();
                return;
            }

            if (args.Any(a => string.Equals(a, "--self-test-stage1", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = Stage1SelfTest.Run();
                return;
            }
            if (args.Any(a => string.Equals(a, "--self-test-500", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = Operational500SelfTest.Run();
                return;
            }

            if (args.Any(a => string.Equals(a, "--self-test-100", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = Operational100SelfTest.Run();
                return;
            }

            if (args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = DatabaseSelfTest.Run();
                return;
            }

            using (var db = Database.Open())
                AdvancedFeaturesMigration.Run(db);
            BackupService.RunAutomaticIfDue();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Idle += (_, _) => AppearanceService.ApplyOpenForms();
            var mainForm = new MainForm();
            NovinStyleMenu.Apply(mainForm);
            AppearanceService.Apply(mainForm);
            Application.Run(mainForm);
        }
    }
}

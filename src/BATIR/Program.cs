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
            AppearanceService.Apply(mainForm);
            Application.Run(mainForm);
        }
    }
}

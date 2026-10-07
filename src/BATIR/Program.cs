using System;
using System.Windows.Forms;

namespace BATIR
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
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

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var mainForm = new MainForm();
            AppearanceService.Apply(mainForm);
            Application.Run(mainForm);
        }
    }
}

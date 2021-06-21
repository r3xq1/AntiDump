namespace AntiDump
{
    using System;
    using System.IO;
    using System.Windows.Forms;
    using dnlib.DotNet;
    using dnlib.DotNet.Writer;
    using Protection.AntiEx;

    internal static class Program
    {
        [STAThread]
        public static void Main()
        {
            Console.Title = "Anti Dumping Tools by r3xq1";
            using var dialog = new OpenFileDialog
            {
                Title = "[.NET Payload] - Выберите файл который хотите добавить...",
                Filter = "Executable File (*.exe)|*.exe",
                AutoUpgradeEnabled = true,
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = Environment.CurrentDirectory,
                RestoreDirectory = true
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                ModuleContext modCtx = ModuleDef.CreateModuleContext();
                using var module = ModuleDefMD.Load(dialog.FileName, modCtx);
                string text2 = Path.GetDirectoryName(dialog.FileName);
                if (text2 != null && !text2.EndsWith("\\")) { text2 += "\\"; }
                string path = $"{text2}{Path.GetFileNameWithoutExtension(dialog.FileName)}_protected{Path.GetExtension(dialog.FileName)}";
                if (module.IsILOnly)
                {
                    ModuleEx.Execute(module);

                    module.Write(path, new ModuleWriterOptions(module)
                    {
                        PEHeadersOptions = { NumberOfRvaAndSizes = 13 },
                        Logger = DummyLogger.NoThrowInstance
                    });
                    Console.WriteLine("Билд создан успешно!");
                }
                else
                {
                    module.NativeWrite(path);
                }
            }
            Console.Read();
        }
    }
}
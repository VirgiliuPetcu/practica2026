using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using WixSharp;
using WixToolset;


namespace GSM_WOL_Installer
{
    internal class Program
    {
        static void Main()
        {
            // 1. Define Project
            Console.WriteLine($"{Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.Parent.FullName}");
            string currentDir = $@"{Directory.GetParent(Environment.CurrentDirectory).Parent.Parent}";
            Console.WriteLine(currentDir);
            var project = new ManagedProject("GSM_WOL",
                new Dir(@"%ProgramFiles%\GSM\GSM_WOL",
                new Files($@"{Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.Parent.FullName}\practica\bin\Release\net9.0\publish\*.*"))); // Ensure this points to your .NET 8 publish folder

            project.GUID = Guid.NewGuid();

            // In Wix 4, ProductId is no longer strictly required/used the same way, 
            // but you can let WixSharp handle the UpgradeCode automatically via the GUID above.

            // 2. Resolve Main Executable
            project.ResolveWildCards();
            var exeFile = project.AllFiles.Single(f => f.Name.EndsWith("practica.exe"));

            project.Version = new Version(FileVersionInfo.GetVersionInfo(exeFile.Name).ProductVersion);
            project.Description = "WOL_GSM Windows Service";
           
            project.ControlPanelInfo.Manufacturer = "Petcu Virgiliu";
            project.LicenceFile = $@"{currentDir}\LicenceDescription.rtf"; // Ensure this file exists in the project directory
            


            // 3. Service Configuration (Syntax remains the same!)
            exeFile.ServiceInstaller = new ServiceInstaller
            {
                Name = "GSM_WOL",
                StartOn = null,
                StopOn = SvcEvent.InstallUninstall_Wait,
                RemoveOn = SvcEvent.Uninstall_Wait,
            };
            project.UI = WUI.WixUI_InstallDir;
            // 4. User Interface
            project.DigitalSignature = new DigitalSignature
            {
                PfxFilePath = $@"{currentDir}\GSMCertificate.pfx",
                Password = "1111"
            };

            // 5. Upgrades
            project.MajorUpgrade = new MajorUpgrade
            {
                Schedule = UpgradeSchedule.afterInstallInitialize,
                DowngradeErrorMessage = "A later version of [ProductName] is already installed. Setup will now exit."
            };

            // 6. Build using the Wix4 specific compiler
            Compiler.BuildMsi(project);
        }
    }
}
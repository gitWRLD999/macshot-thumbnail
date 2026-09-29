using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace MacShotThumbnail;

internal static class StartupRegistration
{
    private static string TaskName => "MacShotThumbnail-" + WindowsIdentity.GetCurrent().User!.Value;
    private static dynamic Connect()
    {
        dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true)!)!;
        service.Connect();
        return service;
    }
    public static bool Enabled
    {
        get
        {
            try
            {
                dynamic service = Connect();
                try { return (bool)service.GetFolder(@"\").GetTask(TaskName).Enabled; }
                finally { Marshal.FinalReleaseComObject(service); }
            }
            catch (COMException)
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                return key?.GetValue("MacShotThumbnail") is string;
            }
        }
        set
        {
            dynamic service = Connect();
            try
            {
                dynamic folder = service.GetFolder(@"\");
                if (value)
                {
                    string executable = Environment.ProcessPath!;
                    string user = WindowsIdentity.GetCurrent().User!.Value;
                    dynamic task = service.NewTask(0);
                    task.RegistrationInfo.Description = "Start MacShot after sign-in and restart it after a failure.";
                    task.Principal.UserId = user;
                    task.Principal.LogonType = 3; // Interactive token: UI runs in the signed-in user's session.
                    task.Principal.RunLevel = 0;
                    task.Settings.DisallowStartIfOnBatteries = false;
                    task.Settings.StopIfGoingOnBatteries = false;
                    task.Settings.ExecutionTimeLimit = "PT0S";
                    task.Settings.StartWhenAvailable = true;
                    task.Settings.MultipleInstances = 2;
                    task.Settings.RestartCount = 3;
                    task.Settings.RestartInterval = "PT1M";
                    dynamic trigger = task.Triggers.Create(9);
                    trigger.UserId = user;
                    trigger.Delay = "PT10S";
                    dynamic action = task.Actions.Create(0);
                    action.Path = executable;
                    action.Arguments = "--supervise";
                    action.WorkingDirectory = Path.GetDirectoryName(executable);
                    folder.RegisterTaskDefinition(TaskName, task, 6, user, null, 3);
                }
                else
                {
                    try { folder.DeleteTask(TaskName, 0); }
                    catch (COMException error) when ((uint)error.HResult == 0x80070002) { }
                }
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key?.DeleteValue("MacShotThumbnail", false);
            }
            finally { Marshal.FinalReleaseComObject(service); }
        }
    }
}

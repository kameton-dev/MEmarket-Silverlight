using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Phone.Management.Deployment;
using Windows.Management.Deployment;

/* --- TODO: ПОПЫТАТЬСЯ ДОВЕСТИ ДО УМА РАЗВЕРТЫВАНИЕ ПРИЛОЖЕНИЙ --- */

namespace MEmarket.Services
{
    public sealed class MEinstaller
    {
        private const string RegistryDll = "api-ms-win-core-registry-l1-1-0.dll";

        [DllImport(RegistryDll, CharSet = CharSet.Unicode, EntryPoint = "RegOpenKeyExW")]
        private static extern int RegOpenKeyEx(IntPtr hKey, string lpSubKey, uint ulOptions, uint samDesired, out IntPtr phkResult);

        [DllImport(RegistryDll, EntryPoint = "RegCloseKey")]
        private static extern int RegCloseKey(IntPtr hKey);

        private static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(unchecked((int)0x80000002));
        private const uint KEY_READ = 0x20019;

        public bool IsInteropUnlocked()
        {
            IntPtr hKey;
            int result = RegOpenKeyEx(HKEY_LOCAL_MACHINE, "SOFTWARE\\Microsoft\\DeviceReg", 0, KEY_READ, out hKey);

            if (result == 0)
            {
                RegCloseKey(hKey);
                return true;
            }
            return false;
        }
        
        public async Task<PackageInstallState> InstallAppAsync(string appTitle, string localFileUri, Action<uint> progressCallback)
        {
            var op = InstallationManager.AddPackageAsync(appTitle, new Uri(localFileUri));

            op.Progress = (sender, progress) =>
            {
                progressCallback?.Invoke(progress);
            };

            var result = await op;
            return result.InstallState;
        }
    }
}
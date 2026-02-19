using Android;
using Android.Content.PM;

namespace XywireHost.UI.Platforms.Android.Permissions;

public class StorageReadPermission : Microsoft.Maui.ApplicationModel.Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        OperatingSystem.IsAndroidVersionAtLeast(33)
            ? new[] 
            {
                (Manifest.Permission.ReadMediaAudio, true),
                (Manifest.Permission.ReadMediaImages, true),
                (Manifest.Permission.ReadMediaVideo, true)
            }
            : new[] 
            {
                (Manifest.Permission.ReadExternalStorage, true)
            };
}

public class StorageWritePermission : Microsoft.Maui.ApplicationModel.Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        OperatingSystem.IsAndroidVersionAtLeast(30)
            ? Array.Empty<(string, bool)>()  // No WRITE_EXTERNAL_STORAGE needed for Android 11+
            : new[] 
            {
                (Manifest.Permission.WriteExternalStorage, true)
            };
}

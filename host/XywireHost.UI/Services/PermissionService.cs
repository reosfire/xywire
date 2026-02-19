namespace XywireHost.UI.Services;

public static class PermissionService
{
    public static async Task<bool> CheckAndRequestStoragePermission()
    {
#if ANDROID
        // On Android 13+, we need READ_MEDIA_* permissions
        // On Android 6-12, we need READ_EXTERNAL_STORAGE
        // When using SAF (Storage Access Framework) pickers, permissions are granted per-file
        // but we still need this for accessing the files after selection
        
        PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
        
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.StorageRead>();
        }

        return status == PermissionStatus.Granted;
#else
        // Other platforms don't require explicit storage permissions
        return true;
#endif
    }

    public static async Task<bool> CheckStoragePermission()
    {
#if ANDROID
        PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.StorageRead>();
        return status == PermissionStatus.Granted;
#else
        return true;
#endif
    }
}

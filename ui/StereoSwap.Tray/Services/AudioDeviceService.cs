using System.Runtime.InteropServices;
using StereoSwap.Tray.Models;

namespace StereoSwap.Tray.Services;

/// <summary>
/// Enumerates WASAPI render endpoints via MMDevice COM (no third-party package).
/// </summary>
public sealed class AudioDeviceService
{
    private static readonly HashSet<string> ExcludedNameFragments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Beacn Mix Create",
        "BEACN Mix Create"
    };

    public IReadOnlyList<AudioRenderDevice> GetRenderDevices()
    {
        var list = new List<AudioRenderDevice>();

        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        enumerator.EnumAudioEndpoints(EDataFlow.eRender, EDeviceState.DEVICE_STATE_ACTIVE, out var collection);
        if (collection is null)
            return list;

        collection.GetCount(out var count);
        string? defaultId = null;
        try
        {
            enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var defaultDev);
            defaultDev.GetId(out defaultId);
            Marshal.ReleaseComObject(defaultDev);
        }
        catch (COMException)
        {
            // No default device.
        }

        for (uint i = 0; i < count; i++)
        {
            collection.Item(i, out var device);
            device.GetId(out var id);
            var name = GetFriendlyName(device);
            var excluded = ExcludedNameFragments.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase));
            list.Add(new AudioRenderDevice
            {
                Id = id ?? string.Empty,
                Name = name,
                IsDefault = !string.IsNullOrEmpty(defaultId) &&
                            string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase),
                IsExcluded = excluded
            });
            Marshal.ReleaseComObject(device);
        }

        Marshal.ReleaseComObject(collection);
        Marshal.ReleaseComObject(enumerator);
        return list;
    }

    private static string GetFriendlyName(IMMDevice device)
    {
        device.OpenPropertyStore(STGM.STGM_READ, out var store);
        try
        {
            var key = PKEY_Device_FriendlyName;
            store.GetValue(ref key, out var prop);
            try
            {
                return PropVariantToString(ref prop) ?? "(unknown device)";
            }
            finally
            {
                PropVariantClear(ref prop);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private static string? PropVariantToString(ref PropVariant prop)
    {
        if (prop.vt == (ushort)VarEnum.VT_LPWSTR)
            return Marshal.PtrToStringUni(prop.pointerValue);
        return null;
    }

    #region COM interop (minimal WASAPI)

    private enum EDataFlow { eRender, eCapture, eAll }
    private enum ERole { eConsole, eMultimedia, eCommunications }
    [Flags] private enum EDeviceState : uint { DEVICE_STATE_ACTIVE = 0x1 }
    private enum STGM { STGM_READ = 0 }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
    }

    // Manual PROPVARIANT layout for VT_LPWSTR read.
    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointerValue;
    }

    private static readonly PropertyKey PKEY_Device_FriendlyName = new()
    {
        fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
        pid = 14
    };

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject;

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, EDeviceState stateMask, out IMMDeviceCollection devices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        int GetCount(out uint count);
        int Item(uint index, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        int OpenPropertyStore(STGM access, out IPropertyStore properties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        int GetCount(out uint count);
        int GetAt(uint index, out PropertyKey key);
        int GetValue(ref PropertyKey key, out PropVariant pv);
        int SetValue(ref PropertyKey key, ref PropVariant pv);
        int Commit();
    }

    #endregion
}

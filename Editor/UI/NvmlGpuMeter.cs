#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using CorridorKey.Editor.Settings;
using UnityEngine;

namespace CorridorKey.Editor.UI
{
    /// <summary>
    /// Loads <c>nvml.dll</c> (NVIDIA driver NVML) with the same resolution order as product policy:
    /// optional user path → plain name → <see cref="Environment.SystemDirectory"/>.
    /// Editor-only; meaningful on Windows with an NVIDIA driver.
    /// </summary>
    public sealed class NvmlGpuMeter : IDisposable
    {
        public const int Success = 0;
        const uint NameBufferBytes = 256u;

        [StructLayout(LayoutKind.Sequential)]
        public struct MemoryInfo
        {
            public ulong Total;
            public ulong Free;
            public ulong Used;
        }

        IntPtr _module;
        NvmlInit? _init;
        NvmlShutdown? _shutdown;
        NvmlDeviceGetHandleByIndex? _getHandle;
        NvmlDeviceGetMemoryInfo? _getMemory;
        NvmlDeviceGetName? _getName;

        IntPtr _device;
        bool _initialized;

        delegate int NvmlInit();
        delegate int NvmlShutdown();
        delegate int NvmlDeviceGetHandleByIndex(uint index, out IntPtr device);
        delegate int NvmlDeviceGetMemoryInfo(IntPtr device, ref MemoryInfo memory);
        delegate int NvmlDeviceGetName(IntPtr device, IntPtr name, uint length);

        NvmlGpuMeter(IntPtr module, NvmlInit init, NvmlShutdown shutdown, NvmlDeviceGetHandleByIndex getHandle,
            NvmlDeviceGetMemoryInfo getMemory, NvmlDeviceGetName getName)
        {
            _module = module;
            _init = init;
            _shutdown = shutdown;
            _getHandle = getHandle;
            _getMemory = getMemory;
            _getName = getName;
        }

        /// <summary>
        /// Try to load NVML and initialize device 0. Returns null if anything fails (missing DLL, no NVIDIA, etc.).
        /// </summary>
        public static NvmlGpuMeter? TryCreate(out string? errorMessage)
        {
            errorMessage = null;
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                errorMessage = "NVML is only used on Windows Editor.";
                return null;
            }

            if (!TryLoadNvmlModule(out var module, out var loadError))
            {
                errorMessage = loadError;
                return null;
            }

            try
            {
                var init = GetDelegate<NvmlInit>(module, "nvmlInit");
                var shutdown = GetDelegate<NvmlShutdown>(module, "nvmlShutdown");
                var getHandle = GetDelegate<NvmlDeviceGetHandleByIndex>(module, "nvmlDeviceGetHandleByIndex");
                var getMemory = GetDelegate<NvmlDeviceGetMemoryInfo>(module, "nvmlDeviceGetMemoryInfo");
                var getName = GetDelegate<NvmlDeviceGetName>(module, "nvmlDeviceGetName");

                if (init == null || shutdown == null || getHandle == null || getMemory == null || getName == null)
                {
                    FreeLibrary(module);
                    errorMessage = "nvml.dll is missing expected exports (NVML entry points).";
                    return null;
                }

                var meter = new NvmlGpuMeter(module, init, shutdown, getHandle, getMemory, getName);
                var ir = init();
                if (ir != Success)
                {
                    meter.Dispose();
                    errorMessage = $"nvmlInit failed ({ir}).";
                    return null;
                }

                meter._initialized = true;
                var hr = getHandle(0, out meter._device);
                if (hr != Success || meter._device == IntPtr.Zero)
                {
                    meter.Dispose();
                    errorMessage = $"nvmlDeviceGetHandleByIndex failed ({hr}).";
                    return null;
                }

                return meter;
            }
            catch (Exception ex)
            {
                FreeLibrary(module);
                errorMessage = ex.Message;
                return null;
            }
        }

        static bool TryLoadNvmlModule(out IntPtr module, out string? error)
        {
            module = IntPtr.Zero;
            error = null;

            // Always try plain "nvml.dll" first (normal DLL search order). Override is only for nonstandard layouts
            // and runs after that fails — so a bad saved path cannot block a working load-by-name.
            var custom = CorridorKeySettings.NvmlDllPath;
            var systemNvml = Path.Combine(Environment.SystemDirectory, "nvml.dll");

            var candidates = new string?[]
            {
                "nvml.dll",
                string.IsNullOrWhiteSpace(custom) ? null : custom.Trim(),
                systemNvml
            };

            foreach (var path in candidates)
            {
                if (string.IsNullOrEmpty(path))
                    continue;
                if (!path.Equals("nvml.dll", StringComparison.OrdinalIgnoreCase) && !File.Exists(path))
                    continue;

                var h = LoadLibrary(path);
                if (h != IntPtr.Zero)
                {
                    module = h;
                    return true;
                }
            }

            error = "Could not load nvml.dll (NVIDIA driver NVML). Tried: nvml.dll by name, custom path (if set), then System32.";
            return false;
        }

        static T? GetDelegate<T>(IntPtr module, string exportName) where T : Delegate
        {
            var p = GetProcAddress(module, exportName);
            if (p == IntPtr.Zero)
                return null;
            return Marshal.GetDelegateForFunctionPointer<T>(p);
        }

        public bool TrySample(out MemoryInfo memory, out string gpuName)
        {
            memory = default;
            gpuName = string.Empty;
            if (!_initialized || _getMemory == null || _getName == null || _device == IntPtr.Zero)
                return false;

            var mr = _getMemory(_device, ref memory);
            if (mr != Success)
                return false;

            var namePtr = Marshal.AllocHGlobal((int)NameBufferBytes);
            try
            {
                var nr = _getName(_device, namePtr, NameBufferBytes);
                if (nr != Success)
                    gpuName = "GPU";
                else
                {
                    var raw = PtrToStringAnsiUpToNull(namePtr);
                    gpuName = ShortenGpuName(raw);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(namePtr);
            }

            return true;
        }

        static string PtrToStringAnsiUpToNull(IntPtr ptr)
        {
            var len = 0;
            while (len < 4096)
            {
                var b = Marshal.ReadByte(ptr, len);
                if (b == 0)
                    break;
                len++;
            }

            if (len == 0)
                return string.Empty;
            var bytes = new byte[len];
            Marshal.Copy(ptr, bytes, 0, len);
            return Encoding.ASCII.GetString(bytes);
        }

        /// <summary>EZ <c>main_window._set_gpu_name</c> parity.</summary>
        public static string ShortenGpuName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            const string geforce = "NVIDIA GeForce ";
            const string nvidia = "NVIDIA ";
            if (name.StartsWith(geforce, StringComparison.OrdinalIgnoreCase))
                return name.Substring(geforce.Length);
            if (name.StartsWith(nvidia, StringComparison.OrdinalIgnoreCase))
                return name.Substring(nvidia.Length);
            return name;
        }

        public void Dispose()
        {
            if (_initialized && _shutdown != null)
            {
                try
                {
                    _shutdown();
                }
                catch
                {
                    // ignore
                }
            }

            _initialized = false;
            _device = IntPtr.Zero;
            _init = null;
            _shutdown = null;
            _getHandle = null;
            _getMemory = null;
            _getName = null;

            if (_module != IntPtr.Zero)
            {
                try
                {
                    FreeLibrary(_module);
                }
                catch
                {
                    // ignore
                }

                _module = IntPtr.Zero;
            }
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32", SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);
    }
}

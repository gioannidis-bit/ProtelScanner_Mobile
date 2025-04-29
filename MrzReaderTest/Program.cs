using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using System.Diagnostics;
using System.Management;

namespace MrzReaderTest
{
    class Program
    {
        // P/Invoke declarations for HidApi functions
        [DllImport("Libs/HidApi.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int hid_init();

        [DllImport("Libs/HidApi.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int hid_exit();

        [DllImport("Libs/HidApi.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr hid_enumerate(ushort vendor_id, ushort product_id);

        [DllImport("Libs/HidApi.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void hid_free_enumeration(IntPtr devs);

        [DllImport("Libs/HidApi.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr hid_open(ushort vendor_id, ushort product_id, string serial_number);

        [DllImport("Libs/HidApi.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void hid_close(IntPtr device);

        static void Main(string[] args)
        {
            Console.WriteLine("=== Access OCR Reader Test ===");

            // Check if DLLs exist in the output directory
            string[] dlls = { "HidApi.dll", "Access_IS_MSR.dll", "HidApiDotNet.dll" };
            foreach (string dll in dlls)
            {
                bool exists = File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dll));
                Console.WriteLine($"{dll}: {(exists ? "Found" : "NOT FOUND")}");
            }

            // List USB devices using System.Management
            Console.WriteLine("\nListing USB devices:");
            try
            {
                var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_USBControllerDevice");
                var collection = searcher.Get();

                List<(string DeviceId, string VID, string PID)> devices = new List<(string, string, string)>();

                foreach (var device in collection)
                {
                    string deviceId = device["Dependent"].ToString();
                    int startIndex = deviceId.IndexOf("DeviceID=\"") + 10;
                    int endIndex = deviceId.IndexOf("\"", startIndex);
                    deviceId = deviceId.Substring(startIndex, endIndex - startIndex);

                    string vid = "";
                    string pid = "";

                    if (deviceId.Contains("VID_") && deviceId.Contains("PID_"))
                    {
                        int vidIndex = deviceId.IndexOf("VID_") + 4;
                        int pidIndex = deviceId.IndexOf("PID_") + 4;

                        vid = deviceId.Substring(vidIndex, 4);
                        pid = deviceId.Substring(pidIndex, 4);

                        devices.Add((deviceId, vid, pid));
                    }
                }

                // Print all devices first
                int count = 0;
                foreach (var device in devices)
                {
                    Console.WriteLine($"Device {++count}: {device.DeviceId}");
                    Console.WriteLine($"  VID: 0x{device.VID}, PID: 0x{device.PID}");
                }

                // Highlight our specific device
                Console.WriteLine("\nLooking for Access OCR Reader (VID: 0x0DB5, PID: 0x013E):");
                bool found = false;
                foreach (var device in devices)
                {
                    if (device.VID.Equals("0DB5", StringComparison.OrdinalIgnoreCase) &&
                        device.PID.Equals("013E", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Found Access OCR Reader: {device.DeviceId}");
                        found = true;
                    }
                }

                if (!found)
                {
                    Console.WriteLine("Access OCR Reader not found in the device list");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing USB devices: {ex.Message}");
            }

            // Test HidApi directly
            Console.WriteLine("\nTesting HidApi directly:");
            try
            {
                // Initialize HidApi
                Console.WriteLine("Initializing HidApi...");
                int result = hid_init();
                Console.WriteLine($"hid_init result: {result} (0 = success)");

                if (result == 0)
                {
                    // Try to find our specific device
                    Console.WriteLine("Enumerating devices...");
                    ushort vendorId = 0x0DB5;  // Access OCR Reader VID
                    ushort productId = 0x013E; // Access OCR Reader PID

                    IntPtr devices = hid_enumerate(vendorId, productId);

                    if (devices != IntPtr.Zero)
                    {
                        Console.WriteLine("Device(s) found! Trying to open...");

                        // Open the device
                        IntPtr device = hid_open(vendorId, productId, null);

                        if (device != IntPtr.Zero)
                        {
                            Console.WriteLine("Successfully opened the device");

                            // Here you would normally set up reading from the device
                            // This is device-specific and may require additional P/Invoke calls

                            // Close when done
                            hid_close(device);
                        }
                        else
                        {
                            Console.WriteLine("Failed to open the device");
                        }

                        // Free enumeration when done
                        hid_free_enumeration(devices);
                    }
                    else
                    {
                        Console.WriteLine("No devices found with the specified VID/PID");
                    }

                    // Cleanup
                    hid_exit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error using HidApi: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }    


static void CheckReaderDlls()
        {
            Console.WriteLine("\n=== Checking for Reader DLLs ===");

            string[] requiredDlls = {
                "HidApi.dll",
                "Access_IS_MSR.dll",
                "HidApiDotNet.dll"
            };

            string currentDir = Directory.GetCurrentDirectory();
            Console.WriteLine($"Current directory: {currentDir}");

            foreach (string dll in requiredDlls)
            {
                string path = Path.Combine(currentDir, dll);
                bool exists = File.Exists(path);
                Console.WriteLine($"  {dll}: {(exists ? "Found" : "NOT FOUND")}");

                if (exists)
                {
                    try
                    {
                        FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(path);
                        Console.WriteLine($"    Version: {versionInfo.FileVersion}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    Error getting version: {ex.Message}");
                    }
                }
            }
        }

        static void LoadHidApiWithReflection()
        {
            Console.WriteLine("\n=== Loading HidApi with Reflection ===");

            try
            {
                // Try to load the assembly directly
                Assembly hidApiAssembly = null;

                try
                {
                    hidApiAssembly = Assembly.LoadFrom("HidApi.dll");
                    Console.WriteLine("Successfully loaded HidApi.dll");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load HidApi.dll directly: {ex.Message}");

                    // Try to find it in already loaded assemblies
                    hidApiAssembly = AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => a.GetName().Name.Contains("HidApi"));

                    if (hidApiAssembly != null)
                    {
                        Console.WriteLine("Found HidApi in loaded assemblies");
                    }
                }

                if (hidApiAssembly == null)
                {
                    Console.WriteLine("Could not load HidApi assembly");
                    return;
                }

                // Explore the assembly
                Console.WriteLine("Exploring HidApi assembly types:");
                foreach (Type type in hidApiAssembly.GetExportedTypes())
                {
                    Console.WriteLine($"  Type: {type.FullName}");

                    // Look for Init method
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        Console.WriteLine($"    Method: {method.Name}");

                        if (method.Name == "Init" || method.Name == "hid_init")
                        {
                            Console.WriteLine("    Found Init method! Trying to invoke...");
                            try
                            {
                                method.Invoke(null, null);
                                Console.WriteLine("    Successfully initialized HidApi!");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"    Failed to invoke Init method: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in reflection: {ex.Message}");
            }
        }

        static void TryDirectHidApiCall()
        {
            Console.WriteLine("\n=== Trying Direct HidApi Call ===");

            try
            {
                int result = hid_init();
                Console.WriteLine($"HidApi init result: {result} (0 means success)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to call hid_init directly: {ex.Message}");
            }
        }

        static void ListUsbDevices()
        {
            Console.WriteLine("\n=== Listing USB Devices ===");

            // Method 1: Using Windows Management
            try
            {
                Console.WriteLine("Method 1: Using WMI to list USB devices");

                var searcher = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_USBControllerDevice");
                var collection = searcher.Get();

                int count = 0;
                foreach (var device in collection)
                {
                    try
                    {
                        string deviceId = device["Dependent"].ToString();
                        deviceId = deviceId.Substring(deviceId.IndexOf("DeviceID=\"") + 10);
                        deviceId = deviceId.Substring(0, deviceId.IndexOf("\""));

                        Console.WriteLine($"  Device {++count}: {deviceId}");

                        // Try to extract VID/PID
                        if (deviceId.Contains("VID_") && deviceId.Contains("PID_"))
                        {
                            int vidIndex = deviceId.IndexOf("VID_") + 4;
                            int pidIndex = deviceId.IndexOf("PID_") + 4;

                            string vid = deviceId.Substring(vidIndex, 4);
                            string pid = deviceId.Substring(pidIndex, 4);

                            Console.WriteLine($"    VID: 0x{vid}, PID: 0x{pid}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    Error parsing device: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing USB devices: {ex.Message}");

                // If System.Management is not available
                if (ex.Message.Contains("System.Management"))
                {
                    Console.WriteLine("You may need to add a reference to System.Management.dll");
                }
            }

            // Method 2: Using SerialPort API
            try
            {
                Console.WriteLine("\nMethod 2: Listing COM ports");
                string[] ports = System.IO.Ports.SerialPort.GetPortNames();

                if (ports.Length == 0)
                {
                    Console.WriteLine("  No COM ports found");
                }
                else
                {
                    foreach (string port in ports)
                    {
                        Console.WriteLine($"  COM Port: {port}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing COM ports: {ex.Message}");
            }
        }

        static void TestMrzReader()
        {
            Console.WriteLine("\n=== Testing Access OCR Reader ===");

            try
            {
                // Initialize HidApi
                int result = hid_init();
                if (result != 0)
                {
                    Console.WriteLine("Failed to initialize HidApi");
                    return;
                }

                // Use the VID/PID from your Access OCR Reader
                ushort vendorId = 0x0DB5;  // Your Access OCR Reader VID
                ushort productId = 0x013E; // Your Access OCR Reader PID

                Console.WriteLine($"Looking for device with VID: 0x{vendorId:X4}, PID: 0x{productId:X4}");

                // Try to enumerate and find your specific device
                // This syntax might need adjustment based on your specific HidApi implementation
                try
                {
                    // Call Enumerate using reflection to handle different possible APIs
                    Console.WriteLine("Attempting to find the device using reflection...");

                    var hidApiAssembly = Assembly.LoadFrom("HidApi.dll");
                    var hidApiType = hidApiAssembly.GetTypes()
                        .FirstOrDefault(t => t.Name.Contains("HidApi") || t.Name.Contains("Hid"));

                    if (hidApiType != null)
                    {
                        var enumerateMethod = hidApiType.GetMethods()
                            .FirstOrDefault(m => m.Name.Contains("Enumerate"));

                        if (enumerateMethod != null)
                        {
                            Console.WriteLine($"Found Enumerate method in {hidApiType.Name}");

                            // Convert parameters to match the expected types
                            object[] parameters;
                            if (enumerateMethod.GetParameters().Length == 2)
                            {
                                parameters = new object[] { vendorId, productId };
                            }
                            else
                            {
                                parameters = new object[] { };
                            }

                            // Call the Enumerate method
                            var devices = enumerateMethod.Invoke(null, parameters);

                            if (devices != null)
                            {
                                // Try to handle the result based on common patterns
                                if (devices is System.Collections.IEnumerable enumerable)
                                {
                                    int deviceCount = 0;
                                    foreach (var device in enumerable)
                                    {
                                        deviceCount++;
                                        Console.WriteLine($"Found device {deviceCount}");

                                        // Try to get properties of the device
                                        var deviceType = device.GetType();
                                        var props = deviceType.GetProperties();

                                        foreach (var prop in props)
                                        {
                                            try
                                            {
                                                var value = prop.GetValue(device);
                                                Console.WriteLine($"  {prop.Name}: {value}");
                                            }
                                            catch { }
                                        }

                                        // Try to open the device
                                        var openMethod = deviceType.GetMethod("Open");
                                        if (openMethod != null)
                                        {
                                            Console.WriteLine("Attempting to open the device...");
                                            try
                                            {
                                                var handle = openMethod.Invoke(device, null);
                                                Console.WriteLine("Device opened successfully!");

                                                // Try to set up data reception
                                                var handleType = handle.GetType();
                                                var dataReceivedEvent = handleType.GetEvent("DataReceived");

                                                if (dataReceivedEvent != null)
                                                {
                                                    Console.WriteLine("Device supports data reception.");
                                                    Console.WriteLine("Waiting for 10 seconds for data...");

                                                    // Wait for some time to see if data comes in
                                                    System.Threading.Thread.Sleep(10000);
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"Error opening device: {ex.Message}");
                                            }
                                        }
                                    }

                                    if (deviceCount == 0)
                                    {
                                        Console.WriteLine("No devices found with the specified VID/PID");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Enumerate returned a {devices.GetType().Name}, not an enumerable collection");
                                }
                            }
                            else
                            {
                                Console.WriteLine("Enumerate method returned null");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Could not find Enumerate method");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Could not find HidApi type");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accessing device with reflection: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error testing Access OCR Reader: {ex.Message}");
            }
        }


    }
}
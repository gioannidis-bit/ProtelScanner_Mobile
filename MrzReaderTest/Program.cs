using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using System.Diagnostics;
using System.Management;
namespace MrzReaderTest
{
    class Program
    {
        // Define delegate types for the callbacks
        private delegate void msrDelegate(ref uint Parameter, [MarshalAs(UnmanagedType.LPStr)] StringBuilder data, int dataSize);
        private delegate void msrConnectionDelegate(ref uint Parameter, bool connectionStatus);

        // P/Invoke declarations for Access_IS_MSR.dll
        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern void initialiseMsr(bool managedCode);

        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern void msrRelease();

        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern bool enableMSR();

        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern bool disableMSR();

        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.LPStr)]
        private static extern string getDeviceName();

        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool registerMSRCallback(msrDelegate Callback, ref uint Parameter);

        [DllImport("Access_IS_MSR.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern bool registerMSRConnectionCallback(msrConnectionDelegate Callback, ref uint Parameter);

        // Keep these delegates as fields to prevent garbage collection
        private static msrDelegate _msrData;
        private static msrConnectionDelegate _msrDataConnection;



        static void Main(string[] args)
        {

            Console.WriteLine($"Running as {(Environment.Is64BitProcess ? "64-bit" : "32-bit")} process");
            Console.WriteLine($"On {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")} operating system");
            Console.WriteLine("=== Access IS MRZ Reader Test ===");

            // Check for required DLLs
            CheckRequiredDlls();

            try
            {
                Console.WriteLine("\nInitializing MRZ reader...");
                InitializeReader();

                Console.WriteLine("\nReader initialized. Please scan a document...");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();

                // Clean up
                ReleaseReader();
                Console.WriteLine("\nReader released. Exiting...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void CheckRequiredDlls()
        {
            string[] requiredDlls = { "Access_IS_MSR.dll", "HidApi.dll", "MrzParser.dll" };

            foreach (string dll in requiredDlls)
            {
                bool exists = File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dll));
                Console.WriteLine($"{dll}: {(exists ? "Found" : "NOT FOUND - REQUIRED")}");

                if (!exists)
                {
                    Console.WriteLine($"Please copy {dll} to: {AppDomain.CurrentDomain.BaseDirectory}");
                }
            }
        }

        static void InitializeReader()
        {
            uint val = 0;

            try
            {
                // Initialize the MSR reader
                initialiseMsr(true);
                Console.WriteLine("MSR reader initialized");

                // Set up callback handlers
                _msrData = MsrCallback;
                _msrDataConnection = MsrConnectionCallback;

                // Register callbacks
                bool callbackRegistered = registerMSRCallback(_msrData, ref val);
                Console.WriteLine($"Data callback registered: {callbackRegistered}");

                bool connectionCallbackRegistered = registerMSRConnectionCallback(_msrDataConnection, ref val);
                Console.WriteLine($"Connection callback registered: {connectionCallbackRegistered}");

                // Try to get device name
                try
                {
                    string deviceName = getDeviceName();
                    Console.WriteLine($"Device name: {deviceName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not get device name: {ex.Message}");
                }

                // Enable the reader
                bool enabled = enableMSR();
                Console.WriteLine($"Reader enabled: {enabled}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing reader: {ex.Message}");
                throw;
            }
        }

        static void ReleaseReader()
        {
            try
            {
                uint val = 0;

                // Unregister callbacks
                registerMSRCallback(null, ref val);
                registerMSRConnectionCallback(null, ref val);

                // Clear callback references
                _msrData = null;
                _msrDataConnection = null;

                // Release the MSR reader
                msrRelease();

                Console.WriteLine("Reader released successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error releasing reader: {ex.Message}");
            }
        }

        // Callback method for scanner data
        private static void MsrCallback(ref uint Parameter, [MarshalAs(UnmanagedType.LPStr)] StringBuilder data, int dataSize)
        {
            Console.WriteLine("\n*** Document scanned! ***");
            Console.WriteLine($"Data received ({dataSize} bytes):");
            string mrzData = data.ToString();
            Console.WriteLine(mrzData);

            // Process the data (parse MRZ, etc.)
            ProcessMrzData(mrzData);
        }

        // Callback method for connection status
        private static void MsrConnectionCallback(ref uint Parameter, bool connectionStatus)
        {
            Console.WriteLine($"\nConnection status changed: {connectionStatus}");
        }

        // Process the MRZ data
        private static void ProcessMrzData(string mrzData)
        {
            // For now, just display the data
            Console.WriteLine("\nProcessing MRZ data...");

            // Split into lines for better readability
            string[] lines = mrzData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            Console.WriteLine("\nMRZ Data by line:");
            for (int i = 0; i < lines.Length; i++)
            {
                Console.WriteLine($"Line {i + 1}: {lines[i]}");
            }

            // Here you would normally use MrzParser to parse the data
            // But for this test we'll just display it
        }
    }
}
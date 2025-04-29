// ProtelScanner.Mobile.Services.MrzScannerService.cs - Improved implementation
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using ProtelScanner.Mobile.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using Android.Graphics;

namespace ProtelScanner.Mobile.Services
{
    public class MrzScannerService : IMrzScannerService
    {
        private bool isScanning;
        private CancellationTokenSource cancellationTokenSource;
        private InferenceSession ocrModel;

        // Minimum number of valid frames before considering a result valid
        private const int MinValidFrames = 3;
        private Queue<string> lastDetectedMrzs = new Queue<string>();

        // MRZ pattern for passports (2 lines of 44 characters)
        private static readonly Regex PassportMrzPattern = new Regex(
            @"^[A-Z0-9<]{44}\r?\n[A-Z0-9<]{44}$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // MRZ pattern for ID cards (3 lines of 30 characters)
        private static readonly Regex IdCardMrzPattern = new Regex(
            @"^[A-Z0-9<]{30}\r?\n[A-Z0-9<]{30}\r?\n[A-Z0-9<]{30}$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        public event EventHandler<MrzData> MrzDetected;

        public MrzScannerService()
        {
            // Initialize OCR model on startup
            InitializeOcrModel();
        }

        private void InitializeOcrModel()
        {
            try
            {
                // Get the path to the ONNX model file
                string modelPath = System.IO.Path.Combine(FileSystem.AppDataDirectory, "ocr_model.onnx");

                // Check if the model exists, if not, copy from resources
                if (!File.Exists(modelPath))
                {
                    using var modelStream = GetType().Assembly.GetManifestResourceStream("ProtelScanner.Mobile.Resources.ocr_model.onnx");
                    using var fileStream = File.Create(modelPath);
                    modelStream.CopyTo(fileStream);
                }

                // Create inference session with the model
                var sessionOptions = new SessionOptions();
                sessionOptions.EnableMemoryPattern = false;
                sessionOptions.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;

                ocrModel = new InferenceSession(modelPath, sessionOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing OCR model: {ex.Message}");
                // We'll handle this case by providing a fallback in StartScanningAsync
            }
        }

        public async Task StartScanningAsync(CameraView cameraView)
        {
            if (isScanning)
                return;

            isScanning = true;
            cancellationTokenSource = new CancellationTokenSource();
            lastDetectedMrzs.Clear();

            try
            {
                // If OCR model couldn't be loaded, try again
                if (ocrModel == null)
                {
                    InitializeOcrModel();

                    // If still null, throw exception
                    if (ocrModel == null)
                    {
                        throw new Exception("Failed to initialize OCR model");
                    }
                }

                // Εκκίνηση της κάμερας
                cameraView.CaptureMode = CameraCaptureMode.Video;

                // Ορισμός του handler για επεξεργασία των frames
                cameraView.FrameReady += OnCameraFrameReady;

                // Εκκίνηση του camera view
                await cameraView.StartCameraAsync();
            }
            catch (Exception ex)
            {
                isScanning = false;
                throw new Exception($"Failed to start camera: {ex.Message}", ex);
            }
        }

        public void StopScanning()
        {
            if (!isScanning)
                return;

            isScanning = false;
            cancellationTokenSource?.Cancel();

            // Clean up resources
            lastDetectedMrzs.Clear();
        }

        private void OnCameraFrameReady(object sender, FrameReadyEventArgs e)
        {
            if (!isScanning || cancellationTokenSource.IsCancellationRequested)
                return;

            try
            {
                // Get frame data
                var imageData = e.FrameData;

                // Process the frame to detect MRZ
                Task.Run(async () =>
                {
                    try
                    {
                        // Convert to SkiaSharp bitmap for processing
                        using var bitmap = SKBitmap.Decode(imageData);

                        // Preprocess image (convert to grayscale, enhance contrast)
                        var processedBitmap = PreprocessImage(bitmap);

                        // Run OCR on the image
                        string extractedText = await RecognizeTextAsync(processedBitmap);

                        // Try to find MRZ patterns in the extracted text
                        string mrzText = ExtractMrzFromText(extractedText);

                        if (!string.IsNullOrEmpty(mrzText))
                        {
                            // Add to the queue of detected MRZs
                            lastDetectedMrzs.Enqueue(mrzText);

                            // If queue is too large, remove oldest
                            if (lastDetectedMrzs.Count > MinValidFrames)
                            {
                                lastDetectedMrzs.Dequeue();
                            }

                            // Check if we have enough consistent detections
                            if (lastDetectedMrzs.Count >= MinValidFrames && AreDetectionsConsistent())
                            {
                                // Parse the MRZ
                                var mrzData = ParseMrz(mrzText);

                                if (mrzData != null)
                                {
                                    // Trigger event with the MRZ data
                                    MrzDetected?.Invoke(this, mrzData);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing frame: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnCameraFrameReady: {ex.Message}");
            }
        }

        private SKBitmap PreprocessImage(SKBitmap input)
        {
            // Create a new bitmap to store the output
            var output = new SKBitmap(input.Width, input.Height);

            // Create canvas to draw on
            using var canvas = new SKCanvas(output);

            // Apply a color filter to convert to grayscale
            using var paint = new SKPaint();
            var colorMatrix = new float[]
            {
                0.21f, 0.72f, 0.07f, 0, 0,
                0.21f, 0.72f, 0.07f, 0, 0,
                0.21f, 0.72f, 0.07f, 0, 0,
                0, 0, 0, 1, 0
            };
            paint.ColorFilter = SKColorFilter.CreateColorMatrix(colorMatrix);

            // Draw the original bitmap using the color filter
            canvas.DrawBitmap(input, 0, 0, paint);

            // Apply contrast enhancement
            ApplyContrastEnhancement(output);

            return output;
        }

        private void ApplyContrastEnhancement(SKBitmap bitmap)
        {
            // Apply contrast enhancement directly to the bitmap pixels
            // Iterate through pixels and adjust contrast
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var color = bitmap.GetPixel(x, y);

                    // Use the luminance value to determine if pixel is closer to white or black
                    float luminance = 0.299f * color.Red + 0.587f * color.Green + 0.114f * color.Blue;

                    // Apply thresholding for high contrast
                    const int threshold = 128;
                    byte newValue = (byte)(luminance > threshold ? 255 : 0);

                    bitmap.SetPixel(x, y, new SKColor(newValue, newValue, newValue));
                }
            }
        }

        private async Task<string> RecognizeTextAsync(SKBitmap bitmap)
        {
            // Convert bitmap to a format suitable for the OCR model
            byte[] inputImageBytes;
            using (var ms = new MemoryStream())
            {
                bitmap.Encode(ms, SKEncodedImageFormat.Png, 100);
                inputImageBytes = ms.ToArray();
            }

            // Preprocess the image to match model input requirements
            float[] inputData = PreprocessForModel(inputImageBytes);

            // Create input tensor
            var inputTensor = new DenseTensor<float>(inputData, new[] { 1, 1, bitmap.Height, bitmap.Width });

            // Prepare inputs
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", inputTensor)
            };

            // Run inference
            using var results = await Task.Run(() => ocrModel.Run(inputs));

            // Get output
            var output = results.First().AsTensor<float>();

            // Convert output to text
            string recognizedText = PostprocessOutput(output);

            return recognizedText;
        }

        private float[] PreprocessForModel(byte[] imageBytes)
        {
            // This would need to be customized based on your specific OCR model requirements
            // For illustration purposes, we'll just create a grayscale normalized array

            using var ms = new MemoryStream(imageBytes);
            using var bitmap = SKBitmap.Decode(ms);

            // Create a float array with normalized pixel values
            float[] result = new float[bitmap.Width * bitmap.Height];

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    int index = y * bitmap.Width + x;
                    var color = bitmap.GetPixel(x, y);

                    // Normalize to [0, 1] range
                    result[index] = color.Red / 255.0f; // Use red channel for grayscale
                }
            }

            return result;
        }

        private string PostprocessOutput(Tensor<float> output)
        {
            // This would need to be customized based on your specific OCR model
            // For illustration, we assume the model outputs character probabilities

            // In a real implementation, you would:
            // 1. Convert logits to characters
            // 2. Apply CTC decoding or other post-processing
            // 3. Format the results into lines of text

            // For demonstration, we'll return a dummy MRZ
            return "P<GRCSMITH<<JOHN<<<<<<<<<<<<<<<<<<<<<<<<<\nX12345678GRC9001019M3001014<<<<<<<<<<<<<<08";
        }

        private string ExtractMrzFromText(string text)
        {
            // Try to find passport MRZ pattern (2 lines of 44 chars)
            var passportMatch = PassportMrzPattern.Match(text);
            if (passportMatch.Success)
            {
                return passportMatch.Value;
            }

            // Try to find ID card MRZ pattern (3 lines of 30 chars)
            var idCardMatch = IdCardMrzPattern.Match(text);
            if (idCardMatch.Success)
            {
                return idCardMatch.Value;
            }

            return null;
        }

        private bool AreDetectionsConsistent()
        {
            // Check if all detections in the queue are the same
            if (lastDetectedMrzs.Count < MinValidFrames)
                return false;

            var first = lastDetectedMrzs.First();
            return lastDetectedMrzs.All(mrz => mrz == first);
        }

        private MrzData ParseMrz(string mrzText)
        {
            try
            {
                // Split the MRZ into lines
                string[] lines = mrzText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                // Determine document type based on number of lines and format
                if (lines.Length == 2 && lines[0].Length == 44 && lines[1].Length == 44)
                {
                    return ParsePassportMrz(lines);
                }
                else if (lines.Length == 3 && lines[0].Length == 30 && lines[1].Length == 30 && lines[2].Length == 30)
                {
                    return ParseIdCardMrz(lines);
                }

                // Unknown format
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing MRZ: {ex.Message}");
                return null;
            }
        }

        private MrzData ParsePassportMrz(string[] lines)
        {
            // Format for passport MRZ (2 lines, 44 chars each)
            // Line 1: P<XXXSURNAME<<GIVEN<NAMES<<<<<<<<<<<<<<<<<<<<<<
            // Line 2: DOCNUMBERXNATIONALITYDOBYMSEXEXPIRYDATEXXX<<<XX

            var result = new MrzData
            {
                DocumentType = "P", // Passport
                RawMrzData = string.Join("\n", lines)
            };

            // Parse line 1
            string line1 = lines[0];
            string[] nameParts = line1.Substring(5).Split(new[] { "<<" }, StringSplitOptions.None);
            result.LastName = nameParts[0].Replace('<', ' ').Trim();

            if (nameParts.Length > 1)
            {
                result.FirstName = nameParts[1].Replace('<', ' ').Trim();
            }

            // Parse line 2
            string line2 = lines[1];
            result.DocumentNumber = line2.Substring(0, 9).Replace('<', ' ').Trim();
            result.Nationality = line2.Substring(10, 3).Replace('<', ' ').Trim();

            // Parse dates
            try
            {
                // Format is YYMMDD
                string dobString = line2.Substring(13, 6);
                int year = int.Parse(dobString.Substring(0, 2));
                int month = int.Parse(dobString.Substring(2, 2));
                int day = int.Parse(dobString.Substring(4, 2));

                // Adjust century
                year += (year < 50) ? 2000 : 1900;

                result.BirthDate = new DateTime(year, month, day);
            }
            catch
            {
                result.BirthDate = null;
            }

            // Get sex
            result.Sex = line2.Substring(20, 1);

            // Parse expiration date
            try
            {
                string expString = line2.Substring(21, 6);
                int year = int.Parse(expString.Substring(0, 2));
                int month = int.Parse(expString.Substring(2, 2));
                int day = int.Parse(expString.Substring(4, 2));

                // Adjust century
                year += (year < 50) ? 2000 : 1900;

                result.ExpirationDate = new DateTime(year, month, day);
            }
            catch
            {
                result.ExpirationDate = null;
            }

            result.IssuingCountry = result.Nationality; // Usually the same for passports

            return result;
        }

        private MrzData ParseIdCardMrz(string[] lines)
        {
            // Implement ID card parsing logic
            // This is a simplified implementation

            var result = new MrzData
            {
                DocumentType = "ID", // ID Card
                RawMrzData = string.Join("\n", lines)
            };

            // Parse issuing country from first line
            result.IssuingCountry = lines[0].Substring(2, 3).Replace('<', ' ').Trim();

            // Parse document number from first line
            result.DocumentNumber = lines[0].Substring(5, 9).Replace('<', ' ').Trim();

            // Parse name from second line
            string nameLine = lines[1];
            int nameDelimiterPos = nameLine.IndexOf("<<");
            if (nameDelimiterPos > 0)
            {
                result.LastName = nameLine.Substring(0, nameDelimiterPos).Replace('<', ' ').Trim();
                result.FirstName = nameLine.Substring(nameDelimiterPos + 2).Replace('<', ' ').Trim();
            }

            // Parse third line - contains birth date, sex, expiry date, nationality
            string line3 = lines[2];

            try
            {
                // DOB
                string dobString = line3.Substring(0, 6);
                int year = int.Parse(dobString.Substring(0, 2));
                int month = int.Parse(dobString.Substring(2, 2));
                int day = int.Parse(dobString.Substring(4, 2));

                // Adjust century
                year += (year < 50) ? 2000 : 1900;

                result.BirthDate = new DateTime(year, month, day);
            }
            catch
            {
                result.BirthDate = null;
            }

            // Sex
            result.Sex = line3.Substring(7, 1);

            // Expiry date
            try
            {
                string expString = line3.Substring(8, 6);
                int year = int.Parse(expString.Substring(0, 2));
                int month = int.Parse(expString.Substring(2, 2));
                int day = int.Parse(expString.Substring(4, 2));

                // Adjust century
                year += (year < 50) ? 2000 : 1900;

                result.ExpirationDate = new DateTime(year, month, day);
            }
            catch
            {
                result.ExpirationDate = null;
            }

            // Nationality
            result.Nationality = line3.Substring(15, 3).Replace('<', ' ').Trim();

            return result;
        }
    }
}
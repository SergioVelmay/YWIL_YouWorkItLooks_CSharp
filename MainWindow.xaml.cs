using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Intel.RealSense;
using Newtonsoft.Json;

namespace YWIL_YouWorkItLooks
{
    public partial class MainWindow : Window
    {
        private readonly Pipeline pipeline;

        private readonly CancellationTokenSource tokenSource = new CancellationTokenSource();

        const int frameRate = 30;
        const int imageEach = 30;

        const int frameWidth = 640;
        const int frameHeight = 480;

        private Int32Rect rect = new Int32Rect(0, 0, frameWidth, frameHeight);

        private int cropLeft = (frameWidth - frameHeight) / 2;

        private Int32Rect region = new Int32Rect((frameWidth - frameHeight) / 2, 0, frameHeight, frameHeight);

        private double scale = 416d / frameHeight;

        private const bool capturingFlag = false;

        private List<string> modelFolders = new List<string>()
        {
            "ClassificationMultilabel",
            "ObjectDetection",
            "ClassificationMulticlass"
        };

        private List<string> multilabelLabels = new List<string>()
        {
            "Step0", 
            "Step1", 
            "Step2", 
            "Step3", 
            "true", 
            "false", 
            "Step4", 
            "front", 
            "back"
        };

        private List<string> objectDetectionLabels = new List<string>()
        {
            "Step5.hole",
            "Step5.part",
            "Step5.error",
            "Step6.hole",
            "Step6.part"
        };

        private List<string> multiclassLabels = new List<string>()
        {
            "Step7.start",
            "Step7.true",
            "Step7.false.A",
            "Step7.false.B"
        };

        private List<string> helpMessages = new List<string>()
        {
            "Place all green parts",
            "Place the orange part",
            "Place the gear on top"
        };

        private List<string> errorMessages = new List<string>()
        {
            "Remove the marked part"
        };

        private List<Uri> stepCheckingUris = new List<Uri>()
        {
            new Uri("pack://application:,,,/Resources/StepNo.png"),
            new Uri("pack://application:,,,/Resources/StepYes.png")
        };

        private Uri GetImageStepUri(int step)
        {
            return new Uri($"pack://application:,,,/Resources/Step{step}.jpg");
        }

        private int frameCount = 0;
        private string lastFramePath;

        private int currentModel = 1; // 0 --> 01234 / 1 --> 56 / 2 --> 7
        private int currentStep = 5;

        private int currentHelp = 0;
        private int currentError = 0;

        private Action<VideoFrame> UpdateImage(Image image)
        {
            WriteableBitmap bitmap = image.Source as WriteableBitmap;

            return new Action<VideoFrame>(frame =>
            {
                bitmap.WritePixels(rect, frame.Data, frame.Stride * frame.Height, frame.Stride);

                frameCount++;

                if (frameCount % imageEach == 0)
                {
                    _ = ProcessBitmapFrame(bitmap);
                }
            });
        }

        private async Task ProcessBitmapFrame(BitmapSource bitmap)
        {
            BitmapSource cropped = new CroppedBitmap(bitmap, region);

            TransformedBitmap scaled = new TransformedBitmap(cropped, new ScaleTransform(scale, scale));

            System.IO.Stream stream = StreamFromBitmapSource(scaled);

            SaveStreamAsPngFile(stream, capturingFlag);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            string result = await ModelInference(modelFolders[currentModel]);

            stopwatch.Stop();
            int inferenceTime = (int)stopwatch.ElapsedMilliseconds;

            switch (currentModel)
            {
                case 0:
                    List<Classification> multilabel = JsonConvert.DeserializeObject<List<Classification>>(result);
                    ShowWindowsMessages(multilabel, inferenceTime);
                    break;
                case 1:
                    List<Detection> objectDetection = JsonConvert.DeserializeObject<List<Detection>>(result);
                    ShowAllDetectionBoxes(objectDetection, inferenceTime);
                    break;
                case 2:
                    ClearDetectionBoxes();
                    List<Classification> multiclass = JsonConvert.DeserializeObject<List<Classification>>(result);
                    ShowWindowsMessages(multiclass, inferenceTime);
                    break;
                default:
                    break;
            }

            stepReferenceImage.Source = new BitmapImage(GetImageStepUri(currentStep));
        }

        private void ShowWindowsMessages<T>(List<T> items, int milliseconds, bool error = false) where T : Classification
        {
            string helpMessage = string.Empty;

            string stepInfo = "Current step detections:" + Environment.NewLine;

            foreach (T item in items)
            {
                stepInfo += Environment.NewLine + " " + item.PredictionToString();
            }

            if (error)
            {
                helpMessage += errorMessages[currentError].ToUpper();
            }
            else
            {
                helpMessage += helpMessages[currentHelp].ToUpper();
            }

            helpMessageTextBlock.Text = helpMessage;

            stepInfoTextBlock.Text = stepInfo;

            inferenceTextBlock.Text = "Last inference took " + milliseconds + "ms";
        }

        private void ClearDetectionBoxes()
        {
            objectDetectionCanvas.Children.Clear();
        }

        private void ShowAllDetectionBoxes(IEnumerable<Detection> detectedObjects, int milliseconds)
        {
            ClearDetectionBoxes();

            IEnumerable<Detection> selectedDetections = detectedObjects.Where(x => x.Label.Contains(currentStep.ToString()));

            bool error = false;

            if (selectedDetections.Select(x => x.Label).Contains("Step5.error"))
            {
                stepChekingImage.Source = new BitmapImage(stepCheckingUris[0]);
                error = true;
            }
            else
            {
                stepChekingImage.Source = new BitmapImage(stepCheckingUris[1]);
            }

            if (error)
            {
                ShowObjectDetectionBox(selectedDetections.Where(x => x.Label.Contains("error")).FirstOrDefault());
            }
            else
            {
                foreach (Detection detection in selectedDetections)
                {
                    ShowObjectDetectionBox(detection);
                }
            }

            if (selectedDetections.Count(x => x.Label.Contains("Step5.part")) > 3)
            {
                currentStep++;
                currentHelp++;
            }

            if (selectedDetections.Select(x => x.Label).Contains("Step6.part"))
            {
                currentStep++;
                currentModel++;
                currentHelp++;
            }

            ShowWindowsMessages(detectedObjects.ToList(), milliseconds, error);
        }

        private void ShowObjectDetectionBox(Detection detectedObject)
        {
            double canvasSide = 480;

            float probability = float.Parse(detectedObject.Probability);

            Color color;
            int thick;

            if (detectedObject.Label.Contains("error"))
            {
                color = Colors.OrangeRed;
                thick = 6;
            }
            else if (detectedObject.Label.Contains("hole"))
            {
                color = Colors.White;
                thick = 2;
            }
            else
            { 
                color = Colors.Lime;
                thick = 4;
            }

            objectDetectionCanvas.Children.Add(
                new Border
                {
                    BorderBrush = new SolidColorBrush(color),
                    BorderThickness = new Thickness(thick),
                    Margin = new Thickness(detectedObject.Box.Left * canvasSide + cropLeft,
                                            detectedObject.Box.Top * canvasSide, 0, 0),
                    Width = detectedObject.Box.Width * canvasSide,
                    Height = detectedObject.Box.Height * canvasSide,
                }
            );
        }

        private async Task<string> ModelInference(string modelFolder)
        {
            string workingDir = @$"{System.IO.Directory.GetCurrentDirectory()}\{modelFolder}";
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.WorkingDirectory = workingDir;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = $"/C py script.py -i {lastFramePath}";
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;

            string result = string.Empty;

            using (Process process = Process.Start(startInfo))
            {
                using (System.IO.StreamReader reader = process.StandardOutput)
                {
                    result = await reader.ReadToEndAsync();
                }
            }

            return result;
        }

        private System.IO.Stream StreamFromBitmapSource(BitmapSource bitmap)
        {
            System.IO.Stream stream = new System.IO.MemoryStream();

            BitmapEncoder encoder = new PngBitmapEncoder();

            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            encoder.Save(stream);

            stream.Position = 0;

            return stream;
        }

        private void SaveStreamAsPngFile(System.IO.Stream inputStream, bool capture)
        {
            string dateName = string.Empty;

            if (capture)
            {
                string dateTime = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.CurrentUICulture.DateTimeFormat);

                dateName += "_" + dateTime;
            }

            string fileName = "YWIL_YouWorkItLooks" + dateName + ".png";

            string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\YWIL_YouWorkItLooks";

            System.IO.DirectoryInfo directory = new System.IO.DirectoryInfo(folderPath);

            if (!directory.Exists)
            {
                directory.Create();
            }

            string filePath = System.IO.Path.Combine(folderPath, fileName);

            lastFramePath = filePath;

            using (System.IO.FileStream outputFileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                inputStream.CopyTo(outputFileStream);
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            stepReferenceImage.Source = new BitmapImage(GetImageStepUri(4));

            try
            {
                Action<VideoFrame> updateColor;

                pipeline = new Pipeline();

                Config config = new Config();

                config.EnableStream(Stream.Color, frameWidth, frameHeight, Format.Rgb8, frameRate);

                PipelineProfile profile = pipeline.Start(config);

                SetupWindow(profile, out updateColor);

                SoftwareDevice device = new SoftwareDevice();

                SoftwareSensor colorSensor = device.AddSensor("Color");

                VideoStreamProfile colorProfile = colorSensor.AddVideoStream(new SoftwareVideoStream
                {
                    type = Stream.Color,
                    index = 0,
                    uid = 101,
                    width = frameWidth,
                    height = frameHeight,
                    fps = frameRate,
                    bpp = 3,
                    format = Format.Rgb8,
                    intrinsics = profile.GetStream(Stream.Color).As<VideoStreamProfile>().GetIntrinsics()
                });

                device.SetMatcher(Matchers.Default);

                Syncer syncer = new Syncer();

                colorSensor.Open(colorProfile);

                colorSensor.Start(syncer.SubmitFrame);

                CancellationToken token = tokenSource.Token;

                byte[] colorData = null;

                Task task = Task.Factory.StartNew(() =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        using (FrameSet frames = pipeline.WaitForFrames())
                        {
                            VideoFrame colorFrame = frames.ColorFrame.DisposeWith(frames);

                            colorData = colorData ?? new byte[colorFrame.Stride * colorFrame.Height];

                            colorFrame.CopyTo(colorData);

                            colorSensor.AddVideoFrame(
                                colorData,
                                colorFrame.Stride,
                                colorFrame.BitsPerPixel / 8,
                                colorFrame.Timestamp,
                                colorFrame.TimestampDomain,
                                (int)colorFrame.Number,
                                colorProfile
                            );
                        }

                        using (FrameSet newFrames = syncer.WaitForFrames())
                        {
                            VideoFrame colorFrame = newFrames.ColorFrame.DisposeWith(newFrames);

                            Dispatcher.Invoke(DispatcherPriority.Render, updateColor, colorFrame);
                        }
                    }
                }, token);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);

                Application.Current.Shutdown();
            }
        }

        private void ControlClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            tokenSource.Cancel();
        }

        private void SetupWindow(PipelineProfile pipelineProfile, out Action<VideoFrame> color)
        {
            using (VideoStreamProfile stream = pipelineProfile.GetStream(Stream.Color).As<VideoStreamProfile>())
            {
                colorRealSenseImage.Source = new WriteableBitmap(stream.Width, stream.Height, 96d, 96d, PixelFormats.Rgb24, null);
            }

            color = UpdateImage(colorRealSenseImage);
        }
    }
}

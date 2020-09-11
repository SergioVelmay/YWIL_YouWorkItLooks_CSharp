using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using YWIL_YouWorkItLooks.Steps;
using YWIL_YouWorkItLooks.Steps.Models;

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

        private readonly int cropLeft = (frameWidth - frameHeight) / 2;

        private Int32Rect region = new Int32Rect((frameWidth - frameHeight) / 2, 0, frameHeight, frameHeight);

        private readonly double scale = 416d / frameHeight;

        private bool assemblyCompleted = false;

        private const bool capturingFlag = false;

        private string lastFramePath;

        private int frameCount = 0;

        private int currentStep = 0; // 01234567

        private int currentModel = 0; // 0 --> 01234 / 1 --> 56 / 2 --> 7

        private int currentHelpMessage = 0;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                Config config = new Config();

                config.EnableStream(Stream.Color, frameWidth, frameHeight, Format.Rgb8, frameRate);

                pipeline = new Pipeline();

                PipelineProfile profile = pipeline.Start(config);

                SetupWindow(profile, out Action<VideoFrame> updateColor);

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

                            colorData ??= new byte[colorFrame.Stride * colorFrame.Height];

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

        private Action<VideoFrame> UpdateImage(Image image)
        {
            WriteableBitmap bitmap = image.Source as WriteableBitmap;

            return new Action<VideoFrame>(frame =>
            {
                bitmap.WritePixels(rect, frame.Data, frame.Stride * frame.Height, frame.Stride);

                if (!assemblyCompleted)
                {
                    frameCount++;

                    if (frameCount % imageEach == 0)
                    {
                        _ = ProcessBitmapFrame(bitmap);
                    }
                }
            });
        }

        private async Task ProcessBitmapFrame(BitmapSource bitmap)
        {
            BitmapSource cropped = new CroppedBitmap(bitmap, region);

            TransformedBitmap scaled = new TransformedBitmap(cropped, new ScaleTransform(scale, scale));

            System.IO.Stream stream = StreamFromBitmapSource(scaled);

            SaveStreamAsPngFile(stream, capturingFlag);

            string result = await ModelInference(CommonSteps.ModelFolders[currentModel]);

            stepReferenceImage.Source = new BitmapImage(CommonSteps.GetImageStepUri(currentStep));

            switch (currentModel)
            {
                case 0:
                    List<Classification> multilabel = JsonConvert.DeserializeObject<List<Classification>>(result);
                    ProcessMultilabelResult(multilabel);
                    break;
                case 1:
                    List<Detection> detection = JsonConvert.DeserializeObject<List<Detection>>(result);
                    ProcessDetectionResult(detection);
                    break;
                case 2:
                    List<Classification> multiclass = JsonConvert.DeserializeObject<List<Classification>>(result);
                    ProcessMulticlassResult(multiclass);
                    break;
                default:
                    break;
            }
        }

        private void ShowWindowMessages<T>(string message, List<T> items) where T : Classification
        {
            string stepInfo;

            if (items.Any())
            {
                stepInfo = $"Current detections in Step #{currentStep}:" + Environment.NewLine;
                foreach (T item in items)
                {
                    stepInfo += Environment.NewLine + item.PredictionToString();
                }
            }
            else
            {
                stepInfo = $"No objects detected in Step #{currentStep}." + Environment.NewLine;
            }

            helpMessageTextBlock.Text = message.ToUpper();

            stepInfoTextBlock.Text = stepInfo;
        }

        private void ShowWindowChecking(int checkingValue)
        {
            stepChekingImage.Source = new BitmapImage(CommonSteps.StepCheckingUris[checkingValue]);

            stepBackground.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(CommonSteps.StepCheckingColors[checkingValue]);
        }

        private void ShowWindowProgress()
        {
            Grid stepGrid = FindName($"step{currentStep}Back") as Grid;
            stepGrid.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(CommonSteps.StepCheckingColors[1]);

            Image stepPrev = FindName($"step{currentStep}Check") as Image;
            stepPrev.Source = new BitmapImage(CommonSteps.StepCheckingUris[1]);

            currentStep++;

            Image stepImage = FindName($"step{currentStep}Image") as Image;
            stepImage.Opacity = 1.0;

            Image stepCheck = FindName($"step{currentStep}Check") as Image;
            stepCheck.Source = new BitmapImage(CommonSteps.StepCheckingUris[3]);
        }

        private void ProcessMultilabelResult(IEnumerable<Classification> classificationLabels)
        {
            string message = MultilabelSteps.MultilabelHelpMessages[currentHelpMessage];

            IEnumerable<Classification> selectedDetections = classificationLabels;

            int checkingValue = 2;

            if (currentHelpMessage != 0)
            {
                if (selectedDetections.Select(x => x.Label).Contains(MultilabelSteps.MultilabelLabels[5]))
                {
                    checkingValue = 0;

                    message = MultilabelSteps.MultilabelErrorMessages[0];
                }
                else if (currentHelpMessage == 1 &&
                    (selectedDetections.Select(x => x.Label).Contains(MultilabelSteps.MultilabelLabels[6]) ||
                     selectedDetections.Select(x => x.Label).Contains(MultilabelSteps.MultilabelLabels[9])))
                {
                    checkingValue = 0;

                    message = MultilabelSteps.MultilabelErrorMessages[1];
                }
                else if (selectedDetections.Any())
                {
                    checkingValue = 1;

                    foreach (string label in selectedDetections.Select(x => x.Label))
                    {
                        if (label.Contains(currentStep.ToString()))
                        {
                            ShowWindowProgress();

                            currentHelpMessage++;
                        }
                    }
                }
            }

            ShowWindowChecking(checkingValue);

            ShowWindowMessages(message, classificationLabels.ToList());

            if (currentHelpMessage == 0)
            {
                currentHelpMessage++;
                
                step0Image.Opacity = 1.0;

                step0Check.Source = new BitmapImage(CommonSteps.StepCheckingUris[3]);
            }

            if (currentHelpMessage == MultilabelSteps.MultilabelHelpMessages.Count - 1)
            {
                currentHelpMessage = 0;

                currentModel++;
            }
        }

        private void ProcessDetectionResult(IEnumerable<Detection> detectedObjects)
        {
            string message = DetectionSteps.DetectionHelpMessages[currentHelpMessage];

            IEnumerable<Detection> selectedDetections = detectedObjects
                .Where(x => x.Label.Contains(currentStep.ToString()));

            int checkingValue = 2;

            if (currentHelpMessage != 0)
            {
                ClearDetectionBoxes();

                if (selectedDetections.Select(x => x.Label).Contains(DetectionSteps.DetectionLabels[2]))
                {
                    checkingValue = 0;

                    message = DetectionSteps.DetectionErrorMessages[0];

                    ShowObjectDetectionBox(selectedDetections.Where(x => x.Label.Contains("error")).FirstOrDefault());
                }
                else if (selectedDetections.Any())
                {
                    checkingValue = 1;

                    foreach (Detection detection in selectedDetections)
                    {
                        ShowObjectDetectionBox(detection);
                    }
                }
            }

            ShowWindowChecking(checkingValue);

            ShowWindowMessages(message, detectedObjects.ToList());

            if (currentHelpMessage == 0)
            {
                currentHelpMessage++;
            }

            if (selectedDetections.Count(x => x.Label.Contains(DetectionSteps.DetectionLabels[1])) == 4)
            {
                currentHelpMessage++;

                ShowWindowProgress();
            }

            if (selectedDetections.Select(x => x.Label).Contains(DetectionSteps.DetectionLabels[4]))
            {
                if (currentHelpMessage == DetectionSteps.DetectionHelpMessages.Count - 1)
                {
                    ClearDetectionBoxes();

                    ShowWindowProgress();

                    currentHelpMessage = 0;

                    currentModel++;
                }

                currentHelpMessage++;
            }
        }

        private void ProcessMulticlassResult(IEnumerable<Classification> classificationLabels)
        {
            string message;

            if (currentHelpMessage < MulticlassSteps.MulticlassHelpMessages.Count)
            {
                message = MulticlassSteps.MulticlassHelpMessages[currentHelpMessage];
            }
            else
            {
                message = MulticlassSteps.MulticlassHelpMessages.Last();

                assemblyCompleted = true;

                assemblyCompletedImage.Opacity = 1.0;

                currentHelpMessage = 0;

                currentStep = 0;

                currentModel = 0;
            }

            Classification selectedDetection = classificationLabels.FirstOrDefault();

            int checkingValue = 1;

            if (currentHelpMessage != 0 && currentHelpMessage != MulticlassSteps.MulticlassHelpMessages.Count -1)
            {
                if (selectedDetection != null)
                {
                    if (selectedDetection.Label.Equals(MulticlassSteps.MulticlassLabels[2]))
                    {
                        checkingValue = 0;

                        message = MulticlassSteps.MulticlassErrorMessages[0];
                    }
                    else if (selectedDetection.Label.Equals(MulticlassSteps.MulticlassLabels[3]))
                    {
                        checkingValue = 0;

                        message = MulticlassSteps.MulticlassErrorMessages[1];
                    }
                    else if (selectedDetection.Label.Equals(MulticlassSteps.MulticlassLabels[1]))
                    {
                        currentHelpMessage++;
                    }
                }
                else
                {
                    checkingValue = 2;
                }
            }

            ShowWindowChecking(checkingValue);

            ShowWindowMessages(message, classificationLabels.ToList());

            if (currentHelpMessage == 0)
            {
                currentHelpMessage++;
            }

            if (currentHelpMessage == MulticlassSteps.MulticlassHelpMessages.Count - 1)
            {
                step7Back.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(CommonSteps.StepCheckingColors[1]);

                step7Check.Source = new BitmapImage(CommonSteps.StepCheckingUris[1]);

                currentHelpMessage++;
            }
        }

        private void ClearDetectionBoxes()
        {
            objectDetectionCanvas.Children.Clear();
        }

        private void ShowObjectDetectionBox(Detection detectedObject)
        {
            double canvasSide = 480;

            SolidColorBrush color;
            int thick;

            if (detectedObject.Label.Contains("error"))
            {
                color = (SolidColorBrush)new BrushConverter().ConvertFrom(CommonSteps.StepCheckingColors[0]);
                thick = 6;
            }
            else if (detectedObject.Label.Contains("hole"))
            {
                color = new SolidColorBrush(Colors.White);
                thick = 2;
            }
            else
            { 
                color = (SolidColorBrush)new BrushConverter().ConvertFrom(CommonSteps.StepCheckingColors[1]);
                thick = 4;
            }

            objectDetectionCanvas.Children.Add(
                new Border
                {
                    BorderBrush = color,
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

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDir,
                FileName = "cmd.exe",
                Arguments = $"/C py script.py -i {lastFramePath}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            string result = string.Empty;

            using (Process process = Process.Start(startInfo))
            {
                using System.IO.StreamReader reader = process.StandardOutput;

                result = await reader.ReadToEndAsync();
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

            using System.IO.FileStream outputFileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create);

            inputStream.CopyTo(outputFileStream);
        }
    }
}

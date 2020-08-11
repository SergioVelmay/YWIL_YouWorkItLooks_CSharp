using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Intel.RealSense;

namespace YWIL_YouWorkItLooks
{
    public partial class MainWindow : Window
    {
        private readonly Pipeline pipeline;

        private readonly CancellationTokenSource tokenSource = new CancellationTokenSource();

        private int frameRate = 30;

        private int frameCount = 0;

        private int imageEach = 30;

        private Int32Rect rect = new Int32Rect(0, 0, 1280, 720);

        private int left = (1280 - 720) / 2;

        private Int32Rect region = new Int32Rect((1280 - 720) / 2, 0, 720, 720);

        private double scale = 416d / 720d;

        private const bool capturingFlag = true;

        private Action<VideoFrame> UpdateImage(Image image)
        {
            WriteableBitmap bitmap = image.Source as WriteableBitmap;

            return new Action<VideoFrame>(frame =>
            {
                bitmap.WritePixels(rect, frame.Data, frame.Stride * frame.Height, frame.Stride);

                frameCount++;

                if (frameCount % imageEach == 0)
                {
                    ProcessBitmapFrame(bitmap);
                }
            });
        }

        private void ProcessBitmapFrame(BitmapSource bitmap)
        {
            if (capturingFlag)
            {
                BitmapSource cropped = new CroppedBitmap(bitmap, region);

                TransformedBitmap scaled = new TransformedBitmap(cropped, new ScaleTransform(scale, scale));

                System.IO.Stream stream = StreamFromBitmapSource(scaled);

                SaveStreamAsPngFile(stream);
            }
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

        private void SaveStreamAsPngFile(System.IO.Stream inputStream)
        {
            string dateTime = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.CurrentUICulture.DateTimeFormat);

            string fileName = "YWIL_YouWorkItLooks_" + dateTime + ".png";

            string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\YWIL_YouWorkItLooks";

            System.IO.DirectoryInfo directory = new System.IO.DirectoryInfo(folderPath);

            if (!directory.Exists)
            {
                directory.Create();
            }

            string filePath = System.IO.Path.Combine(folderPath, fileName);

            using (System.IO.FileStream outputFileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
            {
                inputStream.CopyTo(outputFileStream);
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                Action<VideoFrame> updateColor;

                pipeline = new Pipeline();

                Config config = new Config();

                config.EnableStream(Stream.Color, 1280, 720, Format.Rgb8, 30);

                PipelineProfile profile = pipeline.Start(config);

                SetupWindow(profile, out updateColor);

                SoftwareDevice device = new SoftwareDevice();

                SoftwareSensor colorSensor = device.AddSensor("Color");

                VideoStreamProfile colorProfile = colorSensor.AddVideoStream(new SoftwareVideoStream
                {
                    type = Stream.Color,
                    index = 0,
                    uid = 101,
                    width = 1280,
                    height = 720,
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
                colorImage.Source = new WriteableBitmap(stream.Width, stream.Height, 96d, 96d, PixelFormats.Rgb24, null);
            }

            color = UpdateImage(colorImage);
        }
    }
}

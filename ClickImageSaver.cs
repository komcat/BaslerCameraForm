using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Serilog;
using Newtonsoft.Json;

public class ClickImageSaver : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _baseDirectory;
    private const string IMAGE_DIRECTORY = "ClickedImages";
    private const string METADATA_DIRECTORY = "ClickedMetadata";
    private const double PIXEL_SIZE_X = 5.3;
    private const double PIXEL_SIZE_Y = 5.3;

    public ClickImageSaver(ILogger logger)
    {
        _logger = logger.ForContext<ClickImageSaver>();

        // Create directory in Documents folder instead of application directory
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _baseDirectory = Path.Combine(documentsPath, "CameraCaptures", DateTime.Now.ToString("yyyy-MM-dd"));

        _logger.Information("Initializing ClickImageSaver with base directory: {BaseDir}", _baseDirectory);
        InitializeDirectories();
    }

    private void InitializeDirectories()
    {
        try
        {
            string imageDir = Path.Combine(_baseDirectory, IMAGE_DIRECTORY);
            string metadataDir = Path.Combine(_baseDirectory, METADATA_DIRECTORY);

            if (!Directory.Exists(imageDir))
            {
                Directory.CreateDirectory(imageDir);
                _logger.Information("Created image directory: {ImageDir}", imageDir);
            }

            if (!Directory.Exists(metadataDir))
            {
                Directory.CreateDirectory(metadataDir);
                _logger.Information("Created metadata directory: {MetadataDir}", metadataDir);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating directories for clicked images");
            throw;
        }
    }

    public void SaveClickedImage(Bitmap image, MouseLocationEventArgs clickData)
    {
        try
        {
            _logger.Information("Starting SaveClickedImage operation");

            string timestamp = DateTime.Now.ToString("HHmmss-fff");
            string imageFilename = $"click_{timestamp}.png";
            string metadataFilename = $"click_{timestamp}.json";

            string imagePath = Path.Combine(_baseDirectory, IMAGE_DIRECTORY, imageFilename);
            string metadataPath = Path.Combine(_baseDirectory, METADATA_DIRECTORY, metadataFilename);

            _logger.Information("Saving image to: {ImagePath}", imagePath);

            // Save image
            using (var clonedImage = new Bitmap(image))
            {
                clonedImage.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
            }

            _logger.Information("Image saved successfully, creating metadata");

            // Create metadata
            var metadata = new ClickMetadata
            {
                Timestamp = DateTime.Now,
                ImageFile = imageFilename,
                ScreenLocation = new Point2D { X = clickData.ScreenLocation.X, Y = clickData.ScreenLocation.Y },
                ScaledLocation = new Point2D { X = clickData.ScaledLocation.X, Y = clickData.ScaledLocation.Y },
                PhysicalDistance = new PhysicalDistance
                {
                    X = clickData.PhysicalDistanceX,
                    Y = clickData.PhysicalDistanceY,
                    Unit = "micrometers"
                },
                ZoomFactor = clickData.ZoomFactor,
                PixelCalibration = new PixelCalibration
                {
                    XSize = PIXEL_SIZE_X,
                    YSize = PIXEL_SIZE_Y,
                    Unit = "micrometers/pixel"
                },
                ImageDimensions = new ImageDimensions
                {
                    Width = image.Width,
                    Height = image.Height,
                    Unit = "pixels"
                }
            };

            _logger.Information("Saving metadata to: {MetadataPath}", metadataPath);

            // Save metadata
            string jsonString = JsonConvert.SerializeObject(metadata, Formatting.Indented,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DateFormatString = "yyyy-MM-dd HH:mm:ss.fff"
                });
            File.WriteAllText(metadataPath, jsonString);

            _logger.Information("Successfully saved image and metadata");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving clicked image and metadata");
            throw;
        }
    }

    public void Dispose()
    {
        // No resources to dispose currently
    }
}

// Classes for JSON metadata structure with Newtonsoft.Json attributes
// Update the ClickMetadata class with new fields
public class ClickMetadata
{
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonProperty("imageFile")]
    public string ImageFile { get; set; }

    [JsonProperty("screenLocation")]
    public Point2D ScreenLocation { get; set; }

    [JsonProperty("scaledLocation")]
    public Point2D ScaledLocation { get; set; }

    [JsonProperty("physicalDistance")]
    public PhysicalDistance PhysicalDistance { get; set; }

    [JsonProperty("zoomFactor")]
    public float ZoomFactor { get; set; }

    [JsonProperty("pixelCalibration")]
    public PixelCalibration PixelCalibration { get; set; }

    [JsonProperty("imageDimensions")]
    public ImageDimensions ImageDimensions { get; set; }

    [JsonProperty("processId")]
    public int ProcessId { get; set; } = 0;  // Default value of 0

    [JsonProperty("processName")]
    public string ProcessName { get; set; } = null;  // Default value of null
}

public class Point2D
{
    [JsonProperty("x")]
    public int X { get; set; }

    [JsonProperty("y")]
    public int Y { get; set; }
}

public class PhysicalDistance
{
    [JsonProperty("x")]
    public double X { get; set; }

    [JsonProperty("y")]
    public double Y { get; set; }

    [JsonProperty("unit")]
    public string Unit { get; set; }
}

public class PixelCalibration
{
    [JsonProperty("xSize")]
    public double XSize { get; set; }

    [JsonProperty("ySize")]
    public double YSize { get; set; }

    [JsonProperty("unit")]
    public string Unit { get; set; }
}

public class ImageDimensions
{
    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("unit")]
    public string Unit { get; set; }
}
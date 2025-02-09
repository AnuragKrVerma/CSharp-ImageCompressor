using Microsoft.AspNetCore.Mvc;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ImageCompressor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageCompressorController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImageCompressorController> _logger;
    private int quality = 80;
    public ImageCompressorController(IWebHostEnvironment environment, ILogger<ImageCompressorController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("compress")]
    public async Task<IActionResult> CompressImage(IFormFile imageFile)
    {
        if (imageFile == null || imageFile.Length == 0)
        {
            return BadRequest("No image file provided.");
        }

        long quality = 100L-this.quality; // Adjust quality as needed

        var outputFolder = Path.Combine(_environment.ContentRootPath, "outputimage");
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        var outputFilePath = Path.Combine(outputFolder, "compress_" + imageFile.FileName);

        using (var stream = new MemoryStream())
        {
            await imageFile.CopyToAsync(stream);
            using (var image = Image.FromStream(stream))
            {
                var encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);

                var jpegCodec = ImageCodecInfo.GetImageDecoders()
                    .FirstOrDefault(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
                if (jpegCodec == null)
                {
                    return StatusCode(500, "JPEG codec not found.");
                }

                image.Save(outputFilePath, jpegCodec, encoderParameters);
            }
        }

        return Ok(new { FilePath = outputFilePath });
    }


    [HttpPost("compress-folder-recursive")]
    public IActionResult CompressFolderRecursive([FromForm] string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            return BadRequest("Invalid folder path provided.");
        }

        long quality = 100L-this.quality; // Adjust quality as needed
        
        var outputFolder = Path.Combine(_environment.ContentRootPath, "outputimages");
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        var compressedFiles = new List<string>();

        CompressImagesInFolder(folderPath, outputFolder, quality, compressedFiles);

        return Ok(new { CompressedFiles = compressedFiles });
    }

    private void CompressImagesInFolder(string sourceFolder, string outputFolder, long quality, List<string> compressedFiles)
    {
        var imageFiles = Directory.GetFiles(sourceFolder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file => file.EndsWith(".jpg") || file.EndsWith(".jpeg") || file.EndsWith(".png"))
            .ToList();

        foreach (var imageFilePath in imageFiles)
        {
            var relativePath = Path.GetRelativePath(sourceFolder, imageFilePath);
            var outputFilePath = Path.Combine(outputFolder, "compress_" + relativePath);

            var outputFileDirectory = Path.GetDirectoryName(outputFilePath);
            if (!Directory.Exists(outputFileDirectory))
            {
                Directory.CreateDirectory(outputFileDirectory);
            }

            using (var stream = new MemoryStream(System.IO.File.ReadAllBytes(imageFilePath)))
            {
                using (var image = Image.FromStream(stream))
                {
                    var encoderParameters = new EncoderParameters(1);
                    encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);

                    var jpegCodec = ImageCodecInfo.GetImageDecoders()
                        .FirstOrDefault(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
                    if (jpegCodec == null)
                    {
                        throw new Exception("JPEG codec not found.");
                    }

                    image.Save(outputFilePath, jpegCodec, encoderParameters);
                    compressedFiles.Add(outputFilePath);
                    _logger.LogInformation($"Compressed image saved to: {outputFilePath}");
                }
            }
        }

        var subFolders = Directory.GetDirectories(sourceFolder);
        foreach (var subFolder in subFolders)
        {
            var relativeSubFolderPath = Path.GetRelativePath(sourceFolder, subFolder);
            var outputSubFolderPath = Path.Combine(outputFolder, relativeSubFolderPath);
            CompressImagesInFolder(subFolder, outputSubFolderPath, quality, compressedFiles);
        }
    }
}
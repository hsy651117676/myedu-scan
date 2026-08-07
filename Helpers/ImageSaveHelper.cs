using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace ScanTool.Helpers
{
    public static class ImageSaveHelper
    {
        private static readonly ImageCodecInfo _jpegEncoder =
            ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);

        public static void SaveJpeg(Bitmap bmp, string path, long quality = 92)
        {
            var encParams = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);
            bmp.Save(path, _jpegEncoder, encParams);
        }
    }
}
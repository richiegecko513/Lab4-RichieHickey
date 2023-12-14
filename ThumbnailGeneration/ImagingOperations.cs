using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using GrapeCity.Documents.Text;
using GrapeCity.Documents.Drawing;
using GrapeCity.Documents.Imaging;
using System.Drawing;


namespace ThumbnailGeneration
{
    internal class ImagingOperations
    {
        public static string GetConvertedImage(byte[] stream)
        {
            using var bmp = new GcBitmap();
            bmp.Load(stream);
            //add watermark
            var newImg = new GcBitmap();
            newImg.Load(stream);
            using (var g = bmp.CreateGraphics(Color.White))
            {
                g.DrawImage(
                    newImg,
                    new Rectangle(0, 0, (int)bmp.Width, (int)bmp.Height),
                    null,
                    ImageAlign.Default
                    );
                g.DrawString("DOCUMENT", new TextFormat
                {
                    FontSize = 22,
                    ForeColor = Color.FromArgb(128, Color.Yellow),
                    Font = FontCollection.SystemFonts.DefaultFont
                },
                new Rectangle(0, 0, (int)bmp.Width, (int)bmp.Height),
                TextAlignment.Center, ParagraphAlignment.Center, false);
            }
            //convert to greyscale
            bmp.ApplyEffect(GrayscaleEffect.Get(GrayscaleStandard.BT601));
            //resize to thumbnail
            var resizedImg = bmp.Resize(100, 100, InterpolationMode.NearestNeighbor);
            return GetBase64(resizedImg);
        }

        #region helper 
        private static string GetBase64(GcBitmap bmp)
        {
            using MemoryStream m = new MemoryStream();
            bmp.SaveAsPng(m);
            return Convert.ToBase64String(m.ToArray());
        }
        #endregion
    }
}

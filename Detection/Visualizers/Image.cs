using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Peer.Detection.Visualizers {
    public static class ImageVisualizer {

        /// <summary>
        /// Attempt to load the stream as an image and display
        /// Falls back to a 'No Image' default if it fails
        /// </summary>
        /// <param name="stream">A stream that should be an image</param>
        public static void Display(Stream stream) {
            try {
                Image img = Image.FromStream(stream);

                // We want the image centered, but if it is bigger than the image box, it will be cropped off
                // To fix that, we downscale images that are too large such that they fit, but leave other images unscaled
                int w = Program.form.pictureBox.Width;
                int h = Program.form.pictureBox.Height;
                if (img.Width > w || img.Height > h) {
                    int newH = (int)Math.Ceiling((double)w / img.Width * img.Height);
                    if (newH > h) {
                        int newW = (int)Math.Ceiling((double)h / img.Height * img.Width);
                        img = ResizeImage(img, newW, h);
                    } else {
                        img = ResizeImage(img, w, newH);
                    }
                }

                Program.form.pictureBox.Image = img;
            } catch {
                Program.form.pictureBox.Image = global::Peer.Properties.Resources.no_image;
                return;
            }
        }

        /// <summary>
        /// High-quality image resize
        /// </summary>
        /// <param name="image">Input image</param>
        /// <param name="width">Target width</param>
        /// <param name="height">Target height</param>
        /// <returns></returns>
        public static Bitmap ResizeImage(Image image, int width, int height) {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage)) {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes()) {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }

    }
}

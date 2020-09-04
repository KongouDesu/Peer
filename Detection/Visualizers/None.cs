using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Peer.Detection.Visualizers {
    class None {
        /// <summary>
        /// Defaults to the fallback image, ignoring the input stream
        /// </summary>
        /// <param name="stream"></param>
        public static void Display(Stream stream) {
            Program.form.pictureBox.Image = global::Peer.Properties.Resources.no_image;
        }
    }
}

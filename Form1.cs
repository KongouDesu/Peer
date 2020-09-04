using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Peer {
    public partial class Form1 : Form {

        public Form1() {
            InitializeComponent();
            this.openFileDialog1.FileName = "";
        }

        private void menuFileOpen_Click(object sender, EventArgs e) {
            DialogResult result = this.openFileDialog1.ShowDialog();
            Console.WriteLine(this.openFileDialog1.FileName);

            if (result == DialogResult.OK) {
                FileStream file;
                try {
                    file = System.IO.File.Open(this.openFileDialog1.FileName, FileMode.Open, FileAccess.Read);
                } catch {
                    MessageBox.Show("The selected file could not be opened.", "Failed to open file", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                Console.WriteLine("Length: " + file.Length);
                if (file.Length > 1000 * 1000 * 1000) {
                    Console.WriteLine("Confirming file open for large file (1GB+)");
                    DialogResult dialogResult = MessageBox.Show(
                        String.Format("The file you are opening is very large file ({0}).\nProcessing it may take a while.\nContinue?", Program.format_bytes(file.Length)),
                        "File Size Warning", MessageBoxButtons.YesNo);
                    if (dialogResult == DialogResult.No) {
                        return;
                    }
                }
                // Confirmed that we are opening the file, do work now
                this.menuFileOpen.Enabled = false;
                this.menuFileSave.Enabled = false;
                this.menuFileSaveAs.Enabled = false;
                this.pictureBox.Image = global::Peer.Properties.Resources.eye;
                Program.form.listDetections.Tag = null;
                Program.form.listDetections.DataSource = null;
                var t = new Thread(() => LoadThread.LoadFile(file));
                t.Start();
            } else {
                return;
            }
        }

        private void menuFileExit_Click(object sender, EventArgs e) {
            Application.Exit();
        }

        // When the selection changes, update the displayed item
        private void listDetections_SelectedIndexChanged(object sender, EventArgs e) {
            int idx = this.listDetections.SelectedIndex;
            if (idx == -1) {
                this.pictureBox.Image = pictureBox.Image = global::Peer.Properties.Resources.eye;
                return;
            };

            // Get where in the file the item is
            ((long, long), BytewiseDetector) entry = Program.detectionManager[idx];
            if (entry.Item2 != null) {
                (long, long) indices = entry.Item1;
                // Open the file, skip to where our item is
                Stream file = openFileDialog1.OpenFile();
                file.Seek(entry.Item1.Item1, SeekOrigin.Begin);

                // Read the item into a buffer, load it as an image
                byte[] buffer = new byte[indices.Item2 - indices.Item1];
                file.Read(buffer, 0, buffer.Length);
                Stream stream = new MemoryStream(buffer);
                entry.Item2.Display.Invoke(stream);
            } else {
                this.pictureBox.Image = pictureBox.Image = global::Peer.Properties.Resources.eye;
                Console.WriteLine("No entry matching index -- This shouldn't happen");
            }
        }
        // Doubleclicking writes the image to a temporary file and opens it
        private void picture_doubleclick(object sender, EventArgs e) {
            int idx = this.listDetections.SelectedIndex;
            if (pictureBox.Image == null || idx == -1) return;

            // Create a temp file named "peer" + n + ext
            // 'n' is a digit from 0 to 10, 'ext' is the appropriate file extension
            // It first tries to create peer0, if that fails, it tries peer1, etc. up to peer9
            // Typically if you just opened it, the program may lock it (since it has the file open)
            // This should fix most cases where that happens
            FileStream outfile = null;
            String path = null;
            for (int i = 0; i < 10; i++) {
                path = System.IO.Path.GetTempPath() + "peer" + i + Program.detectionManager[idx].Item2.Extension;
                try {
                    outfile = System.IO.File.Open(path, FileMode.Create, FileAccess.Write);
                    break;
                } catch (Exception _err) {
                    //Console.WriteLine("Failed to open temporary file ({0})", err);
                    continue;
                }
            }
            if (outfile == null || path == null) {
                Console.WriteLine("Failed to open ALL temporary files");
                return;
            }


            // Get where in the file the item is
            (long, long) indices = Program.detectionManager[idx].Item1;
            // Open the file, skip to where our item is
            Stream file = openFileDialog1.OpenFile();
            file.Seek(indices.Item1, SeekOrigin.Begin);

            // Read the item into a buffer
            byte[] buffer = new byte[indices.Item2 - indices.Item1];
            file.Read(buffer, 0, buffer.Length);
            // Write buffer back to outfile
            outfile.Write(buffer, 0, buffer.Length);

            outfile.Flush();
            outfile.Dispose();
            System.Diagnostics.Process.Start("file:///" + path);
        }

    }
}
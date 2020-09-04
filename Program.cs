using Peer.Detection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static Peer.Program;

namespace Peer {
    static class Program {

        public static Form1 form;
        public delegate void Status();
        public static DetectionManager detectionManager = new DetectionManager();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            detectionManager.RegisterDetector(new JPGDetector());
            detectionManager.RegisterDetector(new PNGDetector());
            detectionManager.RegisterDetector(new WAVDetector());

            form = new Form1();
            Application.Run(form);
        }

        public static String format_bytes(long amount) {
            String[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            int index = (int)Math.Floor(Math.Log(amount, 1000));
            double number = Math.Round(amount / Math.Pow(1000, index), 2);
            return String.Format("{0}{1}", number, suffixes[index]);
        }
    }

    public class LoadThread {
        public static void LoadFile(FileStream file) {
            byte[] buffer = new byte[4096];
            long remaining = file.Length;

            while (remaining > 0) {
                int read = file.Read(buffer, 0, 4096);
                remaining -= read;

                detectionManager.ProcessBytes(buffer);

                Status s = delegate {
                    Program.form.statusProgressBar.Value = (int)(1000.0 - (double)remaining / file.Length * 1000.0);
                    Program.form.label1.Text = String.Format("Detections ({0})", detectionManager.DetectionCount());
                };
                Program.form.Invoke(s);
            }

            Console.WriteLine("Done reading");
            file.Close();
            file.Dispose();

            //foreach (var item in jpg.Detections()) {
            //    Console.WriteLine("JPG: " + item);
            //}

            List<String> formatted_detections = detectionManager.FormatDetections();

            int i = 0;
            while (true) {
                try {
                    if (detectionManager[i].Item2 == null)
                        break;
                    Console.WriteLine("{0}", detectionManager[i++]);
                } catch (Exception _E) {
                    break;
                }
                
            }

            Status dets = delegate {
                Program.form.listDetections.DataSource = formatted_detections;
                Program.form.menuFileOpen.Enabled = true;
            };
            Program.form.Invoke(dets);

        }
    }

}

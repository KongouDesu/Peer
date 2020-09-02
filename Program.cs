using Peer.Detection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Peer.Program;

namespace Peer
{
    static class Program
    {

        public static Form1 form;
        public delegate void Status();

        public static int idx = 0;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            form = new Form1();
            Application.Run(form);
        }

        public static String format_bytes(long amount)
        {
            String[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB"};
            int index = (int)Math.Floor(Math.Log(amount, 1000));
            double number = Math.Round(amount / Math.Pow(1000, index), 2);
            return String.Format("{0}{1}", number, suffixes[index]);
        }
    }

    public class LoadThread {
        public static void LoadFile(FileStream file) {
            byte[] buffer = new byte[4096];
            long remaining = file.Length;

            PNGDetector png = new PNGDetector();
            JPGDetector jpg = new JPGDetector();
            WAVDetector wav = new WAVDetector();

            while (remaining > 0) {
                int read = file.Read(buffer, 0, 4096);
                remaining -= read;
                
                for (int i = 0; i < read; i++) {
                    png.Process(buffer[i]);
                    jpg.Process(buffer[i]);
                    wav.Process(buffer[i]);
                    idx++;
                }
                Status s = delegate { 
                    Program.form.statusProgressBar.Value = (int)(1000.0 - (double)remaining / file.Length * 1000.0);
                    Program.form.label1.Text = String.Format("Detections ({0})", jpg.Detections().Count + png.Detections().Count + wav.Detections().Count);
                };
                Program.form.Invoke(s);
            }

            Console.WriteLine("Done reading");
            file.Close();
            file.Dispose();

            foreach (var item in png.Detections()) {
                //Console.WriteLine("PNG: " + item);
            }
            //foreach (var item in jpg.Detections()) {
            //    Console.WriteLine("JPG: " + item);
            //}

            List<String> formatted_detections = new List<String>();
            foreach (var item in png.Detections()) {
                formatted_detections.Add(String.Format("{0} \t {1}", png.DisplayName(), format_bytes(item.Item2 - item.Item1)));
            }
            foreach (var item in jpg.Detections()) {
                formatted_detections.Add(String.Format("{0} \t {1}", jpg.DisplayName(), format_bytes(item.Item2 - item.Item1)));
            }
            foreach (var item in wav.Detections()) {
                formatted_detections.Add(String.Format("{0} \t {1}", wav.DisplayName(), format_bytes(item.Item2 - item.Item1)));
            }

            List<(long, long)> detections = png.Detections();
            detections.AddRange(jpg.Detections());
            detections.AddRange(wav.Detections());

            Console.WriteLine("Found {0} PNGs", png.Detections().Count);
            Console.WriteLine("Found {0} JPGs", jpg.Detections().Count);
            Console.WriteLine("Found {0} WAVs", wav.Detections().Count);


            Status dets = delegate {
                Program.form.listDetections.Tag = detections;
                Program.form.listDetections.DataSource = formatted_detections;
                Program.form.menuFileOpen.Enabled = true;
            };
            Program.form.Invoke(dets);

        }
    }

}

// SN Extractor: lists the camera and lens serial numbers of images on an SD card.
// Reads the standard EXIF tags directly, so no exiftool is needed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class ExifReader
{
    const int ExifIfdPointer = 0x8769;   // points from the main tag list to the EXIF tag list
    const int BodySerial = 0xA431;
    const int LensSerial = 0xA435;

    // Returns { camera serial, lens serial }; blanks if not found.
    public static string[] Read(string path)
    {
        string[] serials = { "", "" };
        try
        {
            using (FileStream fs = File.OpenRead(path))
            {
                BinaryReader r = new BinaryReader(fs);
                if (r.ReadByte() == 0xFF && r.ReadByte() == 0xD8)
                    ReadJpeg(r, serials);
            }
        }
        catch (Exception) { }   // unreadable or damaged file: leave blank
        return serials;
    }

    // JPEG: a chain of segments; EXIF data is a TIFF block inside the "APP1" segment.
    static void ReadJpeg(BinaryReader r, string[] serials)
    {
        Stream s = r.BaseStream;
        while (s.Position + 4 <= s.Length)
        {
            if (r.ReadByte() != 0xFF) return;
            int marker = r.ReadByte();
            int length = (r.ReadByte() << 8) | r.ReadByte();   // includes these 2 bytes
            long next = s.Position + length - 2;
            if (marker == 0xE1 && Encoding.ASCII.GetString(r.ReadBytes(6)) == "Exif\0\0")
            {
                ReadTiff(r, s.Position, serials);
                return;
            }
            if (marker == 0xDA) return;   // image data starts: no EXIF
            s.Position = next;
        }
    }

    // TIFF: a header, then a list of tags (an "IFD"). All offsets count from 'start'.
    static void ReadTiff(BinaryReader r, long start, string[] serials)
    {
        Stream s = r.BaseStream;
        s.Position = start;
        byte[] h = r.ReadBytes(8);
        bool bigEndian = h[0] == 'M';   // "MM" = big-endian, "II" = little-endian
        ReadIfd(r, start, start + ToUInt(h, 4, 4, bigEndian), bigEndian, serials, 0);
    }

    static void ReadIfd(BinaryReader r, long start, long ifd, bool bigEndian,
                        string[] serials, int depth)
    {
        Stream s = r.BaseStream;
        s.Position = ifd;
        int count = (int)ToUInt(r.ReadBytes(2), 0, 2, bigEndian);
        byte[] entries = r.ReadBytes(count * 12);   // each tag entry is 12 bytes

        for (int i = 0; i + 12 <= entries.Length; i += 12)
        {
            int tag = (int)ToUInt(entries, i, 2, bigEndian);
            int length = (int)ToUInt(entries, i + 4, 4, bigEndian);
            long value = ToUInt(entries, i + 8, 4, bigEndian);

            if (tag == ExifIfdPointer && depth == 0)
                ReadIfd(r, start, start + value, bigEndian, serials, 1);
            else if ((tag == BodySerial || tag == LensSerial) && length > 0 && length < 256)
            {
                byte[] text;
                if (length <= 4)   // short values are stored in the entry itself
                {
                    text = new byte[length];
                    Array.Copy(entries, i + 8, text, 0, length);
                }
                else
                {
                    s.Position = start + value;
                    text = r.ReadBytes(length);
                }
                string serial = Encoding.ASCII.GetString(text).Trim('\0', ' ');
                serials[tag == BodySerial ? 0 : 1] = serial;
            }
        }
    }

    static long ToUInt(byte[] b, int offset, int size, bool bigEndian)
    {
        long v = 0;
        for (int i = 0; i < size; i++)
            v = (v << 8) | b[bigEndian ? offset + i : offset + size - 1 - i];
        return v;
    }
}

class MainForm : Form
{
    static readonly string[] Extensions = { ".jpg", ".jpeg" };

    readonly ListView list = new ListView();
    readonly Button scanButton = new Button();
    readonly string[] args;

    MainForm(string[] args)
    {
        this.args = args;
        Text = "Serial Number Extractor";
        Width = 440;
        Height = 360;

        list.Dock = DockStyle.Fill;
        list.View = View.Details;
        list.FullRowSelect = true;
        list.Columns.Add("File", 220);
        list.Columns.Add("Camera Serial", 100);
        list.Columns.Add("Lens Serial", 100);
        Controls.Add(list);

        // Added after the list so it docks first and the list fills the space below it.
        Panel bar = new Panel();
        bar.Dock = DockStyle.Top;
        bar.Height = 32;
        bar.Padding = new Padding(4);
        scanButton.Text = "Scan";
        scanButton.Dock = DockStyle.Left;
        scanButton.Click += delegate { StartScan(); };
        bar.Controls.Add(scanButton);
        Controls.Add(bar);

        Shown += delegate { StartScan(); };
    }

    void StartScan()
    {
        // What to scan: a folder dropped onto the exe, otherwise every removable drive.
        List<string> roots = new List<string>();
        if (args.Length > 0)
            roots.Add(args[0]);
        else
            foreach (DriveInfo d in DriveInfo.GetDrives())
                if (d.DriveType == DriveType.Removable && d.IsReady)
                    roots.Add(d.RootDirectory.FullName);

        list.Items.Clear();
        if (roots.Count == 0)
        {
            Text = "No SD card found.";
            return;
        }
        Text = "Serial Number Extractor - scanning...";
        scanButton.Enabled = false;
        // Scan on a background thread so the window stays responsive.
        new Thread(delegate() { Scan(roots); }) { IsBackground = true }.Start();
    }

    void Scan(List<string> roots)
    {
        List<ListViewItem> rows = new List<ListViewItem>();
        foreach (string root in roots)
            ScanFolder(root, root, rows);

        // Only the main (UI) thread may touch the window.
        BeginInvoke((MethodInvoker)delegate
        {
            list.Items.AddRange(rows.ToArray());
            Text = "SN Extractor - " + rows.Count + " images";
            scanButton.Enabled = true;
        });
    }

    void ScanFolder(string root, string folder, List<ListViewItem> rows)
    {
        try
        {
            foreach (string file in Directory.GetFiles(folder))
            {
                if (Array.IndexOf(Extensions, Path.GetExtension(file).ToLowerInvariant()) < 0)
                    continue;
                string[] serials = ExifReader.Read(file);
                string name = file.Substring(root.Length).TrimStart('\\', '/');
                rows.Add(new ListViewItem(new string[] { name, serials[0], serials[1] }));
            }
            foreach (string sub in Directory.GetDirectories(folder))
                ScanFolder(root, sub, rows);
        }
        catch (Exception) { }   // skip folders Windows won't let us open
    }

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.Run(new MainForm(args));
    }
}

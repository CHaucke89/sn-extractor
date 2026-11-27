// Compiled together with SNExtractor.cs to call ExifReader without the GUI.
using System;

static class Probe
{
    static void Main(string[] files)
    {
        foreach (string f in files)
        {
            string[] s = ExifReader.Read(f);
            Console.WriteLine(f + "\t" + s[0] + "\t" + s[1]);
        }
    }
}

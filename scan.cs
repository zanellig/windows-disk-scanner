using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// Parallel disk scanner (FindFirstFileEx + LARGE_FETCH, like WinDirStat 2.x).
// Usage: scan.exe <root> <minMB> <outPrefix>
class Scan
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WIN32_FIND_DATA
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME c, a, w;
        public uint nFileSizeHigh, nFileSizeLow, r0, r1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string alt;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr FindFirstFileExW(string p, int infoLevel, out WIN32_FIND_DATA d, int searchOp, IntPtr f, int flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool FindNextFileW(IntPtr h, out WIN32_FIND_DATA d);
    [DllImport("kernel32.dll")] static extern bool FindClose(IntPtr h);

    const uint DIR = 0x10, REPARSE = 0x400;
    static long minBytes;
    static int denied;
    static ConcurrentBag<Tuple<string, long>> dirs = new ConcurrentBag<Tuple<string, long>>();
    static ConcurrentBag<Tuple<string, long>> files = new ConcurrentBag<Tuple<string, long>>();

    static long Walk(string path, int depth)
    {
        WIN32_FIND_DATA d;
        IntPtr h = FindFirstFileExW(@"\\?\" + path + @"\*", 1 /*Basic*/, out d, 0, IntPtr.Zero, 2 /*LARGE_FETCH*/);
        if (h == new IntPtr(-1)) { Interlocked.Increment(ref denied); return 0; }
        long total = 0;
        var subs = new List<string>();
        do
        {
            if (d.cFileName == "." || d.cFileName == "..") continue;
            if ((d.dwFileAttributes & DIR) != 0)
            {
                if ((d.dwFileAttributes & REPARSE) == 0) subs.Add(path + "\\" + d.cFileName);
            }
            else
            {
                long s = ((long)d.nFileSizeHigh << 32) | d.nFileSizeLow;
                total += s;
                if (s >= minBytes) files.Add(Tuple.Create(path + "\\" + d.cFileName, s));
            }
        } while (FindNextFileW(h, out d));
        FindClose(h);

        if (depth < 6 && subs.Count > 1)
        {
            long acc = 0;
            Parallel.ForEach(subs, s => Interlocked.Add(ref acc, Walk(s, depth + 1)));
            total += acc;
        }
        else foreach (var s in subs) total += Walk(s, depth + 1);

        if (total >= minBytes) dirs.Add(Tuple.Create(path, total));
        return total;
    }

    static void Main(string[] a)
    {
        minBytes = long.Parse(a[1]) << 20;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long t = Walk(a[0].TrimEnd('\\'), 0);
        Func<Tuple<string, long>, string> fmt = x => x.Item2 + "\t" + x.Item1;
        System.IO.File.WriteAllLines(a[2] + "_dirs.tsv", dirs.OrderByDescending(x => x.Item2).Select(fmt));
        System.IO.File.WriteAllLines(a[2] + "_files.tsv", files.OrderByDescending(x => x.Item2).Select(fmt));
        Console.WriteLine("total={0:F1}GB denied={1} secs={2:F1}", t / 1e9, denied, sw.Elapsed.TotalSeconds);
    }
}

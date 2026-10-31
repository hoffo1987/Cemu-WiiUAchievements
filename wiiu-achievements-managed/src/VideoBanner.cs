using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace WiiUAchievements
{
    // Plays a game's banner video (H.264 MP4 under assets/games/<game>/banner.mp4, embedded by
    // build.ps1) through Windows Media Foundation, which ships with Windows: no extra library.
    // A background thread decodes into a double buffer; the window draws the newest frame.
    // Playback follows the ambient setting and pauses while the window is inactive. Any failure
    // (Mono, missing codecs) leaves Failed set and the caller keeps its vector banner.
    public sealed class VideoBanner : IDisposable
    {
        readonly string path;
        readonly double[] dayStarts;
        readonly object gate = new object();
        readonly ManualResetEvent playing = new ManualResetEvent(false);
        Bitmap front, back, blurFront, blurBack;
        const int BlurW = 40, BlurH = 24, Feather = 44;
        byte[] ramp;
        Color[] leftFront, rightFront, leftBack, rightBack;
        Thread thread;
        volatile bool stopping;
        int width, height;
        public bool Failed { get; private set; }
        public int Frames { get; private set; }
        public string Error { get; private set; }
        // Source rows and anchor that must stay visible: the banner height maps exactly onto
        // FocusTop..FocusBottom; FocusX lands at Anchor of the banner width.
        public float FocusTop = 0, FocusBottom = 360, FocusX = 320, Anchor = 0.5f;

        VideoBanner(string path, double[] dayStarts) { this.path = path; this.dayStarts = dayStarts; }

        // dayStarts: seconds where dawn, day, sunset and night begin in the video (optional).
        public static VideoBanner Open(string gameId, string resourceName, double[] dayStarts)
        {
            try
            {
                string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "games", gameId, resourceName);
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WiiUAchievements.Games." + gameId + "." + resourceName))
                {
                    if (stream != null)
                    {
                        // Media Foundation reads files: keep one private copy per content length.
                        string folder = Path.Combine(Path.GetTempPath(), "WiiUAchievements-media");
                        Directory.CreateDirectory(folder);
                        file = Path.Combine(folder, gameId + "-" + stream.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + resourceName);
                        if (!File.Exists(file) || new FileInfo(file).Length != stream.Length)
                        {
                            string temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
                            using (var output = File.Create(temporary)) stream.CopyTo(output);
                            try { if (File.Exists(file)) File.Delete(file); File.Move(temporary, file); }
                            catch (IOException) { if (File.Exists(temporary)) File.Delete(temporary); }
                        }
                    }
                }
                return File.Exists(file) ? new VideoBanner(file, dayStarts) : null;
            }
            catch (Exception) { return null; }
        }

        public bool HasFrame { get { lock (gate) return front != null; } }
        public void SetPlaying(bool on)
        {
            if (Failed || stopping) return;
            Start();
            if (on) playing.Set(); else playing.Reset();
        }
        void Start()
        {
            if (thread != null) return;
            thread = new Thread(Run) { IsBackground = true, Name = "Banner video" };
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        double StartOffset()
        {
            if (dayStarts == null || dayStarts.Length < 4) return 0;
            int hour = DateTime.Now.Hour;
            return hour >= 5 && hour < 9 ? dayStarts[0] : hour >= 9 && hour < 17 ? dayStarts[1] : hour >= 17 && hour < 20 ? dayStarts[2] : dayStarts[3];
        }

        void Run()
        {
            IMFSourceReader reader = null;
            bool started = false;
            try
            {
                MF.MFStartup(MF.Version, 0); started = true;
                IMFAttributes attributes;
                MF.MFCreateAttributes(out attributes, 1);
                attributes.SetUINT32(MF.EnableVideoProcessing, 1);
                MF.MFCreateSourceReaderFromURL(path, attributes, out reader);
                Marshal.ReleaseComObject(attributes);
                reader.SetStreamSelection(MF.AllStreams, false);
                reader.SetStreamSelection(MF.FirstVideoStream, true);
                IMFMediaType type;
                MF.MFCreateMediaType(out type);
                type.SetGUID(MF.MajorType, MF.Video);
                type.SetGUID(MF.Subtype, MF.Rgb32);
                reader.SetCurrentMediaType(MF.FirstVideoStream, IntPtr.Zero, type);
                Marshal.ReleaseComObject(type);
                reader.GetCurrentMediaType(MF.FirstVideoStream, out type);
                long size; type.GetUINT64(MF.FrameSize, out size);
                width = (int)(size >> 32); height = (int)(size & 0xFFFFFFFF);
                int stride = width * 4;
                try { int s; type.GetUINT32(MF.DefaultStride, out s); if (Math.Abs(s) >= width * 4) stride = s; } catch (COMException) { }
                Marshal.ReleaseComObject(type);
                if (width <= 0 || height <= 0) throw new InvalidDataException("Formato video non valido.");
                // Seeking lands on the previous key frame: decode up to the target before showing.
                double startAt = StartOffset();
                long skipUntil = (long)(startAt * 10000000) - 400000;
                Seek(reader, startAt);
                var clock = new Stopwatch();
                long baseline = -1;
                while (!stopping)
                {
                    // The first frame is shown even while paused, so the banner is never empty.
                    if (Frames > 0 && !playing.WaitOne(250)) { baseline = -1; continue; }
                    if (stopping) break;
                    int index, flags; long timestamp; IMFSample sample;
                    reader.ReadSample(MF.FirstVideoStream, 0, out index, out flags, out timestamp, out sample);
                    if ((flags & MF.EndOfStream) != 0)
                    {
                        if (sample != null) Marshal.ReleaseComObject(sample);
                        Seek(reader, 0); baseline = -1; skipUntil = 0; continue;
                    }
                    if (sample == null) continue;
                    if (timestamp < skipUntil) { Marshal.ReleaseComObject(sample); continue; }
                    try { Copy(sample, stride); }
                    finally { Marshal.ReleaseComObject(sample); }
                    if (baseline < 0) { baseline = timestamp; clock.Restart(); }
                    long due = (timestamp - baseline) / 10000 - clock.ElapsedMilliseconds;
                    SizeF requested; float scale;
                    lock (gate) { requested = requestSize; scale = requestScale; }
                    bool composed = scale > 0 && requested.Width > 0;
                    if (composed) Compose(ref composedBack, back, leftBack, rightBack, requested, scale);
                    if (due > 0) Thread.Sleep((int)Math.Min(due, 200));
                    else if (due < -500) { baseline = timestamp; clock.Restart(); }
                    lock (gate)
                    {
                        var swap = front; front = back; back = swap; swap = blurFront; blurFront = blurBack; blurBack = swap;
                        var column = leftFront; leftFront = leftBack; leftBack = column; column = rightFront; rightFront = rightBack; rightBack = column;
                        frontSerial++;
                        if (composed) { swap = composedFront; composedFront = composedBack; composedBack = swap; composedSerial = frontSerial; }
                    }
                    Frames++;
                }
            }
            catch (Exception ex) { Error = ex.Message; Failed = true; }
            finally
            {
                if (reader != null) try { Marshal.ReleaseComObject(reader); } catch (Exception) { }
                if (started) try { MF.MFShutdown(); } catch (Exception) { }
            }
        }
        static void Seek(IMFSourceReader reader, double seconds)
        {
            var position = new PropVariant { Type = 20, Value = (long)(seconds * 10000000) }; // VT_I8, 100 ns units
            reader.SetCurrentPosition(Guid.Empty, ref position);
        }

        void Copy(IMFSample sample, int stride)
        {
            IMFMediaBuffer buffer;
            sample.ConvertToContiguousBuffer(out buffer);
            try
            {
                IntPtr data; int max, length;
                buffer.Lock(out data, out max, out length);
                try
                {
                    if (back == null || back.Width != width || back.Height != height)
                    {
                        if (back != null) back.Dispose();
                        back = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    }
                    int pitch = Math.Abs(stride), row = width * 4;
                    if (length < pitch * (height - 1) + row) return;
                    if (ramp == null || ramp.Length != width)
                    {
                        // Edge feather: the picture fades into the soft fill beside it.
                        ramp = new byte[width];
                        for (int x = 0; x < width; x++) ramp[x] = (byte)(255 * Math.Min(1, Math.Min((x + 0.5f) / Feather, (width - x - 0.5f) / Feather)));
                    }
                    var bits = back.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    try
                    {
                        var line = new byte[row];
                        for (int y = 0; y < height; y++)
                        {
                            // Negative stride means bottom-up rows.
                            int source = (stride < 0 ? height - 1 - y : y) * pitch;
                            Marshal.Copy(IntPtr.Add(data, source), line, 0, row);
                            for (int x = 0; x < width; x++) line[x * 4 + 3] = ramp[x];
                            Marshal.Copy(line, 0, IntPtr.Add(bits.Scan0, y * bits.Stride), row);
                        }
                    }
                    finally { back.UnlockBits(bits); }
                }
                finally { buffer.Unlock(); }
                // A tiny copy, stretched later with bilinear filtering, is a cheap soft blur that
                // fills the banner around the sharp picture and moves with the sky.
                if (blurBack == null) blurBack = new Bitmap(BlurW, BlurH, PixelFormat.Format32bppRgb);
                using (var g = Graphics.FromImage(blurBack))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.DrawImage(back, new Rectangle(0, 0, BlurW, BlurH), 0, 0, width, height, GraphicsUnit.Pixel);
                }
                // Outer columns of the blurred copy: the sky/sea colours continued beside the picture.
                if (leftBack == null) { leftBack = new Color[BlurH]; rightBack = new Color[BlurH]; }
                for (int y = 0; y < BlurH; y++)
                {
                    leftBack[y] = Theme.Mix(blurBack.GetPixel(0, y), blurBack.GetPixel(1, y), 0.5f);
                    rightBack[y] = Theme.Mix(blurBack.GetPixel(BlurW - 1, y), blurBack.GetPixel(BlurW - 2, y), 0.5f);
                }
            }
            finally { Marshal.ReleaseComObject(buffer); }
        }
        // Composed banner frames at the window's device size. The decoder thread composes the
        // next frame (scaled picture plus continued edges) so the window only copies pixels 1:1;
        // the window composes itself only for the first frame, a new size or a paused video.
        Bitmap composedFront, composedBack;
        int frontSerial, composedSerial = -1;
        SizeF requestSize;
        float requestScale;

        static float DeviceScale(Graphics g)
        {
            using (var m = g.Transform) { var e = m.Elements; return Math.Max(0.25f, (float)Math.Sqrt(e[0] * e[0] + e[1] * e[1])); }
        }
        static Size DeviceSize(SizeF logical, float scale) { return new Size(Math.Max(1, (int)Math.Round(logical.Width * scale)), Math.Max(1, (int)Math.Round(logical.Height * scale))); }
        void Compose(ref Bitmap target, Bitmap frame, Color[] left, Color[] right, SizeF logical, float scale)
        {
            var size = DeviceSize(logical, scale);
            if (target == null || target.Size != size)
            {
                if (target != null) target.Dispose();
                target = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            }
            using (var g = Graphics.FromImage(target))
            {
                g.ScaleTransform(scale, scale);
                Paint(g, new RectangleF(0, 0, logical.Width, logical.Height), frame, left, right);
            }
        }
        // The picture keeps its proportions; outside it, the outer colours continue sky and sea.
        void Paint(Graphics g, RectangleF r, Bitmap frame, Color[] left, Color[] right)
        {
            float s = r.Height / Math.Max(1, FocusBottom - FocusTop), vw = width * s, vh = height * s;
            float x = r.X + r.Width * Anchor - FocusX * s;
            if (vw >= r.Width) x = Math.Max(r.Right - vw, Math.Min(r.X, x));
            float y = r.Y - FocusTop * s;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.PixelOffsetMode = PixelOffsetMode.Half;
            using (var wrap = new ImageAttributes())
            {
                wrap.SetWrapMode(WrapMode.TileFlipXY);
                float f = Feather * s;
                if (x + f > r.X) Side(g, left, new RectangleF(r.X - 2, y, x + f - r.X + 2, vh), r);
                if (x + vw - f < r.Right) Side(g, right, new RectangleF(x + vw - f, y, r.Right + 2 - (x + vw - f), vh), r);
                g.DrawImage(frame, Rectangle.Round(new RectangleF(x, y, vw, vh)), 0, 0, width, height, GraphicsUnit.Pixel, wrap);
            }
        }
        // Draws the newest frame into r (rounded by radius). Returns false when no frame exists yet.
        public bool Draw(Graphics g, RectangleF r, float radius)
        {
            lock (gate)
            {
                if (front == null || leftFront == null) return false;
                float scale = DeviceScale(g);
                requestSize = r.Size; requestScale = scale;
                if (composedFront == null || composedFront.Size != DeviceSize(r.Size, scale) || composedSerial != frontSerial)
                {
                    Compose(ref composedFront, front, leftFront, rightFront, r.Size, scale);
                    composedSerial = frontSerial;
                }
                var state = g.Save();
                using (var clip = Theme.Round(r, radius)) g.SetClip(clip, CombineMode.Intersect);
                var interpolation = g.InterpolationMode; var offset = g.PixelOffsetMode;
                g.InterpolationMode = InterpolationMode.NearestNeighbor; g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(composedFront, (float)Math.Round(r.X * scale) / scale, (float)Math.Round(r.Y * scale) / scale, composedFront.Width / scale, composedFront.Height / scale);
                g.InterpolationMode = interpolation; g.PixelOffsetMode = offset;
                g.Restore(state);
                return true;
            }
        }
        static void Side(Graphics g, Color[] column, RectangleF area, RectangleF visible)
        {
            area.Intersect(new RectangleF(visible.X - 2, area.Y, visible.Width + 4, area.Height));
            if (area.Width < 0.5f || area.Height < 1) return;
            var colors = new Color[column.Length + 2];
            var positions = new float[column.Length + 2];
            colors[0] = column[0]; colors[colors.Length - 1] = column[column.Length - 1]; positions[positions.Length - 1] = 1;
            for (int i = 0; i < column.Length; i++) { colors[i + 1] = column[i]; positions[i + 1] = (i + 0.5f) / column.Length; }
            using (var brush = new LinearGradientBrush(new RectangleF(area.X, area.Y - 0.5f, area.Width, area.Height + 1), colors[0], colors[colors.Length - 1], 90f))
            {
                brush.InterpolationColors = new ColorBlend { Colors = colors, Positions = positions };
                g.FillRectangle(brush, area);
            }
        }
        public void Dispose()
        {
            stopping = true; playing.Set();
            if (thread != null && !thread.Join(800)) { }
            lock (gate)
            {
                foreach (var b in new[] { front, back, blurFront, blurBack, composedFront, composedBack }) if (b != null) b.Dispose();
                front = back = blurFront = blurBack = composedFront = composedBack = null;
            }
        }

        // ---------------------------------------------------------------- Media Foundation interop
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public long Value; }

        static class MF
        {
            public const int Version = 0x00020070, EndOfStream = 0x2;
            public const int FirstVideoStream = unchecked((int)0xFFFFFFFC), AllStreams = unchecked((int)0xFFFFFFFE);
            public static readonly Guid EnableVideoProcessing = new Guid("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");
            public static readonly Guid MajorType = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
            public static readonly Guid Subtype = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
            public static readonly Guid FrameSize = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
            public static readonly Guid DefaultStride = new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
            public static readonly Guid Video = new Guid("73646976-0000-0010-8000-00aa00389b71");
            public static readonly Guid Rgb32 = new Guid("00000016-0000-0010-8000-00aa00389b71");
            [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)] public static extern void MFStartup(int version, int flags);
            [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)] public static extern void MFShutdown();
            [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)] public static extern void MFCreateAttributes(out IMFAttributes attributes, int size);
            [DllImport("mfplat.dll", ExactSpelling = true, PreserveSig = false)] public static extern void MFCreateMediaType(out IMFMediaType type);
            [DllImport("mfreadwrite.dll", ExactSpelling = true, PreserveSig = false)]
            public static extern void MFCreateSourceReaderFromURL([MarshalAs(UnmanagedType.LPWStr)] string url, IMFAttributes attributes, out IMFSourceReader reader);
        }

        // Vtable order matters; unused slots are declared but never called.
        [ComImport, Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFAttributes
        {
            void GetItem(); void GetItemType(); void CompareItem(); void Compare();
            void GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out int value);
            void GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out long value);
            void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString();
            void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
            void SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, int value);
            void SetUINT64(); void SetDouble();
            void SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [In, MarshalAs(UnmanagedType.LPStruct)] Guid value);
            void SetString(); void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore(); void GetCount(); void GetItemByIndex(); void CopyAllItems();
        }
        [ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFMediaType
        {
            void GetItem(); void GetItemType(); void CompareItem(); void Compare();
            void GetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out int value);
            void GetUINT64([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, out long value);
            void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString();
            void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
            void SetUINT32([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, int value);
            void SetUINT64(); void SetDouble();
            void SetGUID([In, MarshalAs(UnmanagedType.LPStruct)] Guid key, [In, MarshalAs(UnmanagedType.LPStruct)] Guid value);
            void SetString(); void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore(); void GetCount(); void GetItemByIndex(); void CopyAllItems();
            void GetMajorType(); void IsCompressedFormat(); void IsEqual(); void GetRepresentation(); void FreeRepresentation();
        }
        [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFSample
        {
            void GetItem(); void GetItemType(); void CompareItem(); void Compare(); void GetUINT32(); void GetUINT64();
            void GetDouble(); void GetGUID(); void GetStringLength(); void GetString(); void GetAllocatedString();
            void GetBlobSize(); void GetBlob(); void GetAllocatedBlob(); void GetUnknown(); void SetItem(); void DeleteItem(); void DeleteAllItems();
            void SetUINT32(); void SetUINT64(); void SetDouble(); void SetGUID();
            void SetString(); void SetBlob(); void SetUnknown(); void LockStore(); void UnlockStore(); void GetCount(); void GetItemByIndex(); void CopyAllItems();
            void GetSampleFlags(); void SetSampleFlags(); void GetSampleTime(); void SetSampleTime(); void GetSampleDuration(); void SetSampleDuration();
            void GetBufferCount(); void GetBufferByIndex();
            void ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
            void AddBuffer(); void RemoveBufferByIndex(); void RemoveAllBuffers(); void GetTotalLength(); void CopyToBuffer();
        }
        [ComImport, Guid("045fa593-8799-42b8-bc8d-8968c6453507"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFMediaBuffer
        {
            void Lock(out IntPtr buffer, out int maxLength, out int currentLength);
            void Unlock();
            void GetCurrentLength(); void SetCurrentLength(); void GetMaxLength();
        }
        [ComImport, Guid("70ae66f2-c809-4e4f-8915-bdcb406b7993"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMFSourceReader
        {
            void GetStreamSelection(int streamIndex, [MarshalAs(UnmanagedType.Bool)] out bool selected);
            void SetStreamSelection(int streamIndex, [MarshalAs(UnmanagedType.Bool)] bool selected);
            void GetNativeMediaType(int streamIndex, int typeIndex, out IMFMediaType type);
            void GetCurrentMediaType(int streamIndex, out IMFMediaType type);
            void SetCurrentMediaType(int streamIndex, IntPtr reserved, IMFMediaType type);
            void SetCurrentPosition([In, MarshalAs(UnmanagedType.LPStruct)] Guid timeFormat, [In] ref PropVariant position);
            void ReadSample(int streamIndex, int controlFlags, out int actualStreamIndex, out int streamFlags, out long timestamp, out IMFSample sample);
            void Flush(); void GetServiceForStream(); void GetPresentationAttribute();
        }
    }
}

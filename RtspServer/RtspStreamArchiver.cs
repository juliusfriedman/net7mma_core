using Media.Rtsp.Server.MediaTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Media.Rtsp.Server
{
    public class RtspStreamArchiver : Common.BaseDisposable
    {
        //Nested type for playback

        public readonly string BaseDirectory =
            System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "archive";

        IDictionary<IMedia, RtpTools.RtpDump.Program> Attached =
            new System.Collections.Concurrent.ConcurrentDictionary<IMedia, RtpTools.RtpDump.Program>();
        
        RtspStreamArchiver(bool shouldDispose = true)
            : base(shouldDispose)
        {
            if (System.IO.Directory.Exists(BaseDirectory) is false)
            {
                System.IO.Directory.CreateDirectory(BaseDirectory);
            }
        }

        //Creates directories
        public virtual void Prepare(IMedia stream)
        {
            string path = System.IO.Path.Combine(BaseDirectory, stream.Id.ToString());
            if (System.IO.Directory.Exists(path) is false)
            {
                System.IO.Directory.CreateDirectory(path);
            }

            //Create Toc file?

            //Start, End
        }

        //Determine if directory is created
        public virtual bool IsArchiving(IMedia stream) => Attached.ContainsKey(stream);

        //Writes a .Sdp file
        public virtual async Task WriteDescriptionAsync(IMedia stream,
            Sdp.SessionDescription sdp,
            CancellationToken cancellationToken)
        {
            if (IsArchiving(stream) is false) return;

            //Add lines with Alias info?

            string path = System.IO.Path.Combine(BaseDirectory, stream.Id.ToString(), "SessionDescription.sdp");
            await System.IO.File
                .WriteAllTextAsync(path, sdp.ToString(), cancellationToken)
                .ConfigureAwait(false);
        }

        //Writes a RtpToolEntry for the packet
        public virtual void WritePacket(IMedia stream, Common.IPacket packet)
        {
            if (stream is null) return;

            if (Attached.TryGetValue(stream, out RtpTools.RtpDump.Program program) is false) return;

            if (packet is Rtp.RtpPacket p)
                program.Writer.WritePacket(p);
            else
                program.Writer.WritePacket(packet as Rtcp.RtcpPacket);
        }

        public virtual void Start(IMedia stream, RtpTools.FileFormat format = RtpTools.FileFormat.Binary)
        {
            if (stream is RtpSource p)
            {
                if (Attached.TryGetValue(stream, out RtpTools.RtpDump.Program program)) return;

                Prepare(stream);

                program = new RtpTools.RtpDump.Program(); //.DumpWriter(BaseDirectory + '/' + stream.Id + '/' + DateTime.UtcNow.ToFileTime(), format, new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0));

                Attached.Add(stream, program);

                p.RtpClient.RtpPacketReceieved += RtpClientPacketReceieved;
                p.RtpClient.RtcpPacketReceieved += RtpClientPacketReceieved;
            }
        }

        void RtpClientPacketReceieved(object sender,
            Common.IPacket packet = null,
            Media.Rtp.RtpClient.TransportContext tc = null)
        {
            if (sender is Rtp.RtpClient c)
                WritePacket(Attached.Keys.FirstOrDefault(k => k is RtpSource s && s.RtpClient == c), packet);
        }

        //Stop recoding a stream
        public virtual void Stop(IMedia stream)
        {
            if (stream is RtpSource s)
            {
                if (Attached.TryGetValue(stream, out RtpTools.RtpDump.Program program) is false) return;

                if (program is not null &&
                    Common.IDisposedExtensions.IsNullOrDisposed(program.Writer) is false)
                    program.Writer.Dispose();

                Attached.Remove(stream);

                s.RtpClient.RtpPacketReceieved -= RtpClientPacketReceieved;
                s.RtpClient.RtcpPacketReceieved -= RtpClientPacketReceieved;
            }
        }

        public override void Dispose()
        {
            if (IsDisposed) return;

            base.Dispose();

            foreach (var stream in Attached.Keys.ToArray())
                Stop(stream);

            Attached = null;
        }

        public readonly List<ArchiveSource> Sources = [];

        public class ArchiveSource(string name, Uri source) : SourceMedia(name, source)
        {
            public ArchiveSource(string name, Uri source, Guid id)
                : this(name, source)
            {
                Id = id;
            }
            

            public readonly List<RtpSource> Playback = [];

            public RtpSource CreatePlayback()
            {
                RtpSource created = null;

                Playback.Add(created);

                return created;
            }

            //Implicit operator to RtpSource which creates a new RtpSource configured from the main source using CreatePlayback
        }

        public IMedia FindStreamByLocation(Uri mediaLocation)
        {
            //Check sources for name, if found then return

            return null;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
namespace Media.Container
{
    /// <summary>
    /// Represents the basic logic around all media files including reading bytes and determining the amount of bytes remaining.
    /// Position and Length are cached to improve performance.
    /// </summary>
    public abstract class MediaFileStream : System.IO.FileStream, IDisposable, Media.Common.IDisposed, Container.IMediaContainer
    {
        #region Statics

        private const string CurrentWorkingDirectory = ".", ParentDirectory = "..";

        public static System.IO.FileInfo GetCurrentWorkingDirectory()
        {
            return new System.IO.FileInfo(CurrentWorkingDirectory);
        }

        private static readonly Dictionary<string, MediaFileStream> m_ExtensionMap = [];

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public static bool TryRegisterExtension(string extenstion, MediaFileStream implementation)
        {
            if (string.IsNullOrWhiteSpace(extenstion)) return false;

            if (extenstion[0] == (char)Common.ASCII.Period) extenstion = extenstion.Substring(1);

            try
            {
                m_ExtensionMap.Add(extenstion, implementation);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public static bool TryUnRegisterExtension(string extenstion)
        {
            if (string.IsNullOrWhiteSpace(extenstion)) return false;

            if (extenstion[0] == (char)Common.ASCII.Period) extenstion = extenstion.Substring(1);

            return m_ExtensionMap.Remove(extenstion);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public static IEnumerable<string> GetRegisteredExtensions() { return m_ExtensionMap.Keys; }

        private static readonly Type MediaFileStreamType = typeof(MediaFileStream);
        private static readonly Type[] ConstructorTypes = new Type[] { typeof(Microsoft.Win32.SafeHandles.SafeFileHandle), typeof(System.Uri), typeof(System.IO.FileAccess), typeof(bool), typeof(System.Action) }; //new Type[] { typeof(string), typeof(System.IO.FileAccess) };

        public static MediaFileStream GetCompatbileImplementation(string fileName, AppDomain domain = null, System.IO.FileMode mode = System.IO.FileMode.Open, System.IO.FileAccess access = System.IO.FileAccess.ReadWrite)
        {

            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException("fileName");

            //Determine if to use Extension first?

            //object[] args = new object[] { fileName };

            System.IO.FileStream fs = new(fileName, mode, access);

            object[] args = null;

            bool final = false;

            Action sharedFinalizer = null;

            sharedFinalizer = () =>
            {
                if (false == final) return;

                if (fs is not null)
                {
                    fs.Dispose();

                    fs = null;
                }

                if (args is not null) args = null;

                if (sharedFinalizer is not null) sharedFinalizer = null;
            };

            args = new object[] { fs, new System.Uri(fileName), access, false, sharedFinalizer };

            //Get all loaded assemblies in the current application domain
            foreach (var assembly in (domain ?? AppDomain.CurrentDomain).GetAssemblies())
            {
                //Iterate each derived type which is a SubClassOf RtcpPacket.
                foreach (var derivedType in assembly.GetTypes().Where(t => false == t.IsAbstract && t.IsSubclassOf(MediaFileStreamType)))
                {
                    //OrderBy where the Type.Extensions has an element which corresponds to the extension of the fileName.

                    //If the derivedType is an abstraction then add to the AbstractionBag and continue
                    //if (derivedType.IsAbstract) continue;

                    MediaFileStream created = (MediaFileStream)derivedType.GetConstructor(ConstructorTypes).Invoke(args);

                    using (var rootNode = created.Root)
                        if (rootNode is not null && rootNode.IsComplete)
                        {
                            args = null;

                            //Ensure the finalizer can now run.
                            final = true;

                            return created;
                        }

                    //The created MediaFileStream is useless for this type of file.
                    created.Dispose();

                    //Seek back
                    fs.Seek(0, System.IO.SeekOrigin.Begin);
                }
            }

            fs.Dispose();

            fs = null;

            args = null;

            return null;
        }

        #endregion

        #region Fields

        private bool m_Disposed, m_ShouldDispose = true;
        private readonly Uri m_Source;

        protected internal System.IO.FileInfo FileInfo;

        protected internal long m_Position, m_Length;

        #endregion

        #region Properties

        public bool IsDisposed { get { return m_Disposed; } }

        public bool ShouldDispose { get { return m_ShouldDispose; } protected set { m_ShouldDispose = value; } }

        public Uri Source { get { return IsDisposed ? null : m_Source; } }

        //Should use correct origin when value < 0
        public override long Position { get { return IsDisposed ? -1 : m_Position; } set { if (value == m_Position) return; Seek(value, System.IO.SeekOrigin.Begin); } }

        public override long Length { get { return IsDisposed ? -1 : m_Length; } }

        //-1 instead of 0 when disposed?
        public virtual long Remaining { get { return IsDisposed ? 0 : m_Length - m_Position; } }

        #endregion

        #region Constructor / Destructor

        ~MediaFileStream() { Dispose(m_ShouldDispose); }        //Dispose should call Close if open

        /// <summary>
        /// Creates a new FileStream from the given
        /// </summary>
        /// <param name="location"></param>
        /// <param name="access"></param>
        public MediaFileStream(Uri location, System.IO.FileAccess access = System.IO.FileAccess.Read)
            : base(location.LocalPath, System.IO.FileMode.Open, access, System.IO.FileShare.ReadWrite)
        {
            m_Source = location;

            FileInfo = new System.IO.FileInfo(m_Source.LocalPath);

            m_Position = base.Position;

            m_Length = base.Length;
        }

        public MediaFileStream(System.IO.FileStream stream, System.IO.FileAccess access = System.IO.FileAccess.Read)
            : base(stream.SafeFileHandle, access)
        {
            m_Source = new Uri(stream.Name);
        }

        //Giving a SocketHere is almost pointless even though it can be done, the problem comes when seeking which is what is desired anyway.

        /// <summary>
        /// Creates a new FileStream from the given
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="access"></param>
        public MediaFileStream(string filename, System.IO.FileAccess access = System.IO.FileAccess.Read) : this(new Uri(filename), access) { }

        #endregion        

        #region Buffering implementation..

        //m_RemainingInBuffers = (Max - Position), > 0

        public bool Buffering { get; protected set; }

        //event BufferingComplete

        private readonly Action AfterClose;

        //Could also use FileOptions.DeleteOnClose, Could also just have an Extension method.
        private void DeleteFromFileInfoIfExists()
        {
            if (/*IsDisposed is false && */ FileInfo is not null && FileInfo.Exists) FileInfo.Delete();
        }

        private bool TryDeleteFromFileInfoIfExists()
        {
            try { DeleteFromFileInfoIfExists(); return true; }
            catch { return false; }
        }

        public MediaFileStream(Uri source, System.IO.Stream stream, DateTime? quantifier = null, int size = 8192, bool shouldDelete = true)
            : base(GetCurrentWorkingDirectory().Directory.ToString() + (quantifier.HasValue ? quantifier.Value.ToFileTimeUtc() : DateTime.UtcNow.ToFileTimeUtc()).ToString(), System.IO.FileMode.CreateNew, System.IO.FileAccess.ReadWrite, System.IO.FileShare.ReadWrite)
        {
            m_Source = source;

            FileInfo = new System.IO.FileInfo(Name);

            Buffering = true;

            Common.Extensions.Stream.StreamExtensions.ITransactionResult result = Common.Extensions.Stream.StreamExtensions.BeginCopyTo(stream, this, size, WriteAt);

            //Race, the transaction might have already finished on small files...
            if (result.IsCompleted) IStreamCopyTransactionResultCompleted(this, result);
            else result.TransactionCompleted += IStreamCopyTransactionResultCompleted;

            if (shouldDelete) AfterClose = new Action(DeleteFromFileInfoIfExists);

            //SetEndOfFile
            //SetLength(result.ExpectedLength);

            //Done by WriteAt
            ////result.TransactionWrite += (s, e) =>
            ////{
            ////    m_Position += e.LastRead;

            ////    m_Length = e.Total;

            ////};

            //Wait for the end of the transaction or the root to be valid
            //Buffering && Root is null
            while (result.IsTransactionDone is false && Root is null) result.AsyncWaitHandle.WaitOne(0);
        }

        public MediaFileStream(Uri source, System.IO.Stream stream, DateTime? quantifier = null, int size = 8192, Action afterClose = null)
            : this(source, stream, quantifier, size, false)
        {
            AfterClose = afterClose;
        }

        /// <summary>
        /// Automatically deletes the assoicated temporary file using <see cref="AfterClose"/> and copying all data from the temporary file to the given path.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="stream"></param>
        /// <param name="path"></param>
        /// <param name="mode"></param>
        /// <param name="quantifier"></param>
        /// <param name="size"></param>
        public MediaFileStream(Uri source, System.IO.Stream stream, string path, System.IO.FileMode mode, DateTime? quantifier = null, int size = 8192)
            : this(source, stream, quantifier, size, false)
        {
            //Start position would be useful here for resuming or chunking downloading

            //E.g. for a large file make N readers with offset positions, of N * size, download each part and then write to the result stream when its required

            //Static copy to path func
            AfterClose = () =>
            {
                using (var fs = new System.IO.FileStream(path, mode, System.IO.FileAccess.ReadWrite))
                {
                    this.CopyTo(fs);
                }
            };
        }

        private void IStreamCopyTransactionResultCompleted(object sender, Common.Extensions.Stream.StreamExtensions.ITransactionResult t)
        {
            Buffering = false;

            if (t is not null)
            {
                t.TransactionCompleted -= IStreamCopyTransactionResultCompleted;

                t.Dispose();
            }
        }

        #endregion

        #region Caching implementation...

        //Something like a Node prototype array which is useful for Nodes which have certain identifiers would be useful when encountering nodes,
        //Such nodes would be parsed on a background thread if required for use by others including track reading, it would also reduce IO

        public bool TryApplyCachingPolicy(NodeAction n, bool combine = false, bool eraseCache = true)
        {
            try
            {
                ApplyCachingPolicy(n, combine, eraseCache);

                return true;
            }
            catch { return false; }


        }

        public void ApplyCachingPolicy(NodeAction n, bool combine = false, bool eraseCache = true)
        {
            ArgumentNullException.ThrowIfNull(n);

            Common.IDisposedExtensions.CheckDisposed(this);

            if (combine)
                m_NodeCachingPolicy += n;
            else
                m_NodeCachingPolicy = n;

            if (eraseCache)
                if (m_NodeCache is not null)
                    m_NodeCache.Clear();
                else
                    m_NodeCache = [];
        }

        public void RemoveCachingPolicy() { RemoveCachingPolicy(m_NodeCachingPolicy); }

        public void RemoveCachingPolicy(NodeAction n)
        {
            if (m_NodeCachingPolicy is not null) m_NodeCachingPolicy -= n;
        }

        public delegate bool NodeAction(Node n);

        private NodeAction m_NodeCachingPolicy;
        private SortedDictionary<long, Node> m_NodeCache;

        private bool TryCacheNodeInstance(Node n)
        {
            return TryCacheNode(n, true, true, true);
        }

        private bool IgnoreNode(Node n)
        {
            return true;
        }

        protected virtual void CacheNode(Node n, bool useInstance = false, bool keepData = false, bool checkSelf = true)
        {
            if (n is null) throw new ArgumentNullException("n");

            if (checkSelf && n.Master != this) throw new InvalidOperationException("n.Master is not this => [MediaFileStream] instance.");

            if (m_NodeCache is null) throw new ArgumentNullException("No Caching Polocy");

            m_NodeCache.Add(n.Offset, useInstance ? n : keepData ? Node.CreateNodeWithDataReference(n) : Node.CreateNodeFrom(n));
        }

        protected virtual bool TryCacheNode(Node n, bool useInstance = false, bool keepData = false, bool checkSelf = true)
        {
            try { CacheNode(n, useInstance, keepData, checkSelf); return true; }
            catch { return false; }
        }

        public IEnumerable<KeyValuePair<long, Node>> Cache { get { return m_NodeCache; } }

        public IEnumerable<Node> CachedNodes { get { return m_NodeCache.Values; } }


        //Indexes
        public IEnumerable<long> CachedNodeOffsets { get { return m_NodeCache.Keys; } }

        #endregion

        #region Meta information

        //protected List<Node> TrackNodes, InformationNodes, OtherNodes;

        #endregion

        #region Override Methods

        protected override void Dispose(bool disposing)
        {
            base.Dispose(m_Disposed = disposing || m_ShouldDispose);
        }

        public override void Close()
        {
            if (IsDisposed) return;

            m_Position = m_Length = -1;

            base.Close();

            if (AfterClose is not null) AfterClose();

            FileInfo = null;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public override void SetLength(long value) { base.SetLength(m_Length = value); }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public override long Seek(long offset, System.IO.SeekOrigin origin) { try { return m_Position = base.Seek(offset, origin); } finally { RefreshFileInfo(); } }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public override int Read(byte[] buffer, int offset, int count) { try { int result = base.Read(buffer, offset, count); m_Position += result; return result; } finally { RefreshFileInfo(); } }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public override int ReadByte() { try { int result = base.ReadByte(); if (result != -1) ++m_Position; return result; } finally { RefreshFileInfo(); } }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public override void Write(byte[] array, int offset, int count)
        {
            try
            {
                long total = m_Position + count;
                base.Write(array, offset, count);
                if (total > m_Length) m_Length = total;
                m_Position += count;
            }
            finally { RefreshFileInfo(); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public override void WriteByte(byte value)
        {
            try
            {
                base.WriteByte(value);
                if (m_Position > m_Length) ++m_Length;
                ++m_Position;
            }
            finally { RefreshFileInfo(); }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Given a <see cref="Node"/>, will move to the <see cref="Node.DataOffset"/> and proceed to enumerate all nodes contained within <paramref name="node"/> by using <see cref="Node.DataSize"/>.
        /// <see cref="Position"/> will be restored afterwards.
        /// </summary>
        /// <param name="node">The <see cref="Node"/> to enumerate.</param>
        /// <returns>The enumerator which will consume all contained nodes.</returns>
        public IEnumerable<Node> GetChildren(Node node)
        {
            var position = Position;

            Position = node.DataOffset;

            var remains = node.DataSize;

            var enumerator = GetEnumerator();

            while (remains > 0 && enumerator.MoveNext())
            {
                var next = enumerator.Current;

                yield return next;

                remains -= next.TotalSize;
            }

            Position = position;
        }

        public virtual void RefreshFileInfo(bool updateLength = true)
        {
            //If the FileInfo is not null
            if (FileInfo is not null)
            {
                FileInfo.Refresh();

                if (updateLength) m_Length = FileInfo.Exists ? FileInfo.Length : -1;
            }
        }

        /// <summary>
        /// Reads the given amount of bytes into the buffer. If reading past the end of the stream an exception is thrown.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public virtual byte[] ReadBytes(long count)
        {
            if (count <= 0) return Media.Common.MemorySegment.EmptyBytes;
            byte[] result = new byte[count];
            long i = 0;
            /*do*/
            while ((count -= i += Read(result, (int)i, (int)count)) > 0) ;
            return result;
        }

        //if(m_Position + count > m_Length) count = (m_Position + count - m_Length) to ensure correct amount of byes read

        /// <summary>
        /// Updates <see cref="Position"/> if the given value is positive.
        /// </summary>
        /// <param name="count">The amount of bytes to skip.</param>
        /// <returns><see cref="Position"/></returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public virtual long Skip(long count) { return count /*<=*/== 0 ? Position : Position += count; } //Should also work in reverse... and without the branch although if is 0 is a good check

        /// <summary>
        /// using a new FileStream with ReadOnly access a seek to the given position is performed and a subsequent read at the given position is performed.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns>The amount of bytes read</returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public virtual int ReadAt(long position, byte[] buffer, int offset, int count) { return ReadAt(position, buffer, offset, count, true); }
        public virtual int ReadAt(long position, byte[] buffer, int offset, int count, bool refreshFileInfo = true)
        {
            if (count is 0) return 0;

            using (var stream = new System.IO.FileStream(Name, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
            {
                if (position != stream.Seek(position, System.IO.SeekOrigin.Begin)) throw new InvalidOperationException("Unable to obtain the given position");

                int read = 0; while ((count -= read += stream.Read(buffer, read, count)) > 0) ;

                RefreshFileInfo(refreshFileInfo);

                return read;
            }
        }

        /// <summary>
        /// using a new FileStream with WriteOnly access a seek to the given position is performed and a subsequent write at the given position is performed.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public virtual void WriteAt(long position, byte[] buffer, int offset, int count) { WriteAt(position, buffer, offset, count, true); }
        public virtual void WriteAt(long position, byte[] buffer, int offset, int count, bool refreshFileInfo = true)
        {
            if (count is 0) return;
            using (var stream = new System.IO.FileStream(Name, System.IO.FileMode.Open, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite))
            {
                if (position != stream.Seek(position, System.IO.SeekOrigin.Begin)) throw new InvalidOperationException("Unable to obtain the given position");

                stream.Write(buffer, offset, count);

                //Could manually update m_Lenth

                RefreshFileInfo(refreshFileInfo);
            }
        }

        /// <summary>
        /// using a new FileStream with WriteOnly access a seek to the end is performed and a subsequent write of the given data.
        /// <see cref="Length"/> is updated to reflect the operation when successful and <see cref="FileInfo"/> is Refreshed.
        /// </summary>
        /// <param name="buffer">The data to write</param>
        /// <param name="offset">The offset in data to begin writing</param>
        /// <param name="count">The amount of bytes from <paramref name="offset"/> within <paramref name="data"/></param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public virtual void Append(byte[] buffer, int offset, int count) { WriteAt(Length, buffer, offset, count); }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        protected internal long GetPosition() { return Position; }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        protected internal long GetLength() { return Length; }

        #endregion

        #region Abstraction

        /// <summary>
        /// Identifies the first <see cref="Node"/> found in the stream.
        /// </summary>
        public abstract Node Root { get; }

        //Move to explicit declaration?
        public abstract Node TableOfContents { get; }

        //Could provide a default implemetnation which calls the caching policy

        public abstract IEnumerator<Node> GetEnumerator();

        //Should abstract KnownExtensions? MimeTypes!!!!

        //string[] m_Extensions, m_MimeTypes;
        //OR
        //Common.Collections.Generic.ConcurrentThesaurus<string, string> m_ExtensionToMime = new Common.Collections.Generic.ConcurrentThesaurus<string, string>();

        //Should abstract NodeIdentifierSize, NodeLengthSize(-1 for Variable?), and MinimumNodeSize, MaximumNodeSize?

        /// <summary>
        /// When overriden in a derived class, Provides information for each 'Track' in the Media
        /// </summary>
        public abstract IEnumerable<Track> GetTracks();

        /// <summary>
        /// When overriden in a derived class, retrieves the <see cref="Rtp.RtpFrame"/> related to the given parameters
        /// </summary>
        /// <param name="track">The <see cref="TrackReference"/> which identifies the Track to retrieve the sample data from</param>       
        /// <param name="duration">The amount of time related to the result</param>
        /// <returns>The <see cref="Rtp.RtpFrame"/> containing the sample data</returns>
        public abstract Common.SegmentStream GetSample(Track track, out TimeSpan duration);

        //Enumerable of samples ?

        #endregion

        #region IMediaContainer

        public System.IO.Stream BaseStream { get { return this; } }

        public Uri Location { get { return m_Source; } }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return GetEnumerator(); }

        public virtual string ToTextualConvention(Node node) { return GetType().Name + "-Node"; }

        #endregion
    }


    //public class IdentifierTransaction : Common.Extensions.Stream.StreamExtensions.ReadTransaction
    //{
    //    public IdentifierTransaction(System.IO.Stream s, byte[] dest, int off, int len)
    //        : base(s, dest, off, len)
    //    {

    //    }
    //}

}

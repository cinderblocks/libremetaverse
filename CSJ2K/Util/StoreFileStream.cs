using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CSJ2K.Util
{
    internal sealed class StoreFileStream : Stream
    {
        #region FIELDS

        private readonly IRandomAccessStream _stream;
	    private bool _disposed;

        #endregion

        #region CONSTRUCTORS

	    internal StoreFileStream(string path, string mode)
	    {
		    try
		    {
			    _stream = Task.Run(async () =>
				                             {
					                             StorageFile file;
					                             if (mode.Equals("rw", StringComparison.OrdinalIgnoreCase))
					                             {
						                             file =
							                             await
							                             StorageFile.CreateStreamedFileFromUriAsync(Path.GetFileName(path), new Uri(path),
							                                                                        null);
					                             }
					                             else if (mode.Equals("r", StringComparison.OrdinalIgnoreCase))
					                             {
						                             file = await StorageFile.GetFileFromPathAsync(path);
					                             }
					                             else
					                             {
						                             throw new ArgumentOutOfRangeException("mode", "Only modes r and rw are supported.");
					                             }
					                             return await file.OpenAsync(FileAccessMode.ReadWrite);
				                             }).Result;
			    _disposed = false;
			    Name = path;
		    }
		    catch
		    {
			    _stream = null;
			    _disposed = true;
			    Name = String.Empty;
		    }
	    }

	    #endregion

		#region PROPERTIES

	    public override bool CanRead
	    {
		    get { return true; }
	    }

	    public override bool CanSeek
	    {
		    get { return true; }
	    }

	    public override bool CanWrite
	    {
		    get { return true; }
	    }

	    public override long Length
	    {
			get
			{
				if (_disposed) throw new ObjectDisposedException("_stream");
				return (long)_stream.Size;
			}
	    }

	    public override long Position
	    {
			get
			{
				if (_disposed) throw new ObjectDisposedException("_stream");
				return (long)_stream.Position;
			}
			set
			{
				if (_disposed) throw new ObjectDisposedException("_stream");
				_stream.Seek((ulong)value);
			}
	    }

	    internal string Name { get; private set; }

		#endregion

		#region METHODS

	    public override void Flush()
	    {
			if (_disposed) throw new ObjectDisposedException("_stream");
			Task.Run(async () => await _stream.FlushAsync()).Wait();
	    }

	    public override int Read(byte[] buffer, int offset, int count)
	    {
			if (_disposed) throw new ObjectDisposedException("_stream");
			return Task.Run(async () =>
			                          {
										  using (var reader = new DataReader(_stream))
										  {
											  await reader.LoadAsync((uint)count);
											  var length = Math.Min(count, (int)reader.UnconsumedBufferLength);
											  var temp = new byte[length];
											  reader.ReadBytes(temp);
											  Array.Copy(temp, 0, buffer, offset, length);
											  reader.DetachStream();
											  return length;
										  }
			                          }).Result;
	    }

	    public override long Seek(long offset, SeekOrigin origin)
	    {
			if (_disposed) throw new ObjectDisposedException("_stream");
			switch (origin)
		    {
			    case SeekOrigin.Begin:
				    _stream.Seek((ulong)offset);
				    break;
			    case SeekOrigin.Current:
				    _stream.Seek(_stream.Position + (ulong)offset);
				    break;
			    case SeekOrigin.End:
				    _stream.Seek(_stream.Size - (ulong)offset);
				    break;
			    default:
				    throw new ArgumentOutOfRangeException("offset");
		    }
		    return (long)_stream.Position;
	    }

	    public override void SetLength(long value)
	    {
			if (_disposed) throw new ObjectDisposedException("_stream");
			_stream.Size = (ulong)value;
	    }

	    public override void Write(byte[] buffer, int offset, int count)
	    {
			if (_disposed) throw new ObjectDisposedException("_stream");
		    Task.Run(async () =>
			                   {
				                   using (var writer = new DataWriter(_stream))
				                   {
					                   var temp = new byte[count];
									   Array.Copy(buffer, offset, temp, 0, count);
					                   writer.WriteBytes(temp);
					                   await writer.StoreAsync();
					                   writer.DetachStream();
				                   }
			                   }).Wait();
	    }

		protected override void Dispose(bool disposing)
		{
			if (_disposed) return;

			if (disposing)
			{
				Flush();
				_stream.Dispose();
			}
			_disposed = true;

			base.Dispose(disposing);
		}

	    #endregion
    }
}
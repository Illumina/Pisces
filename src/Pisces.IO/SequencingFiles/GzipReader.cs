using System;
using System.Collections.Generic;
using System.IO;
using SequencingFiles.Compression;

namespace SequencingFiles
{
	public class GzipReader : GzipCommon
	{
		#region member variables
		private const uint BufferSize = 131072;
		private readonly char[] _stringBuffer;
		private int _bufferByteCount;
		#endregion

		/// <summary>
		///     constructor
		/// </summary>
		public GzipReader(string filename)
		{
			LineBuffer = new byte[BufferSize];
			_stringBuffer = new char[BufferSize];
			_bufferByteCount = 0;
			Open(filename);
		}

		/// <summary>
		///     Closes the FASTQ file
		/// </summary>
		public override void Close()
		{
			if (IsOpen)
			{
				IsOpen = false;
				SafeNativeMethods.gzclose(FileStreamPointer);
			}
		}

		/// <summary>
		///     Fills the buffer
		/// </summary>
		/// <param name="bufferOffset"></param>
		public void FillBuffer(int bufferOffset)
		{
			_bufferByteCount = SafeNativeMethods.gzreadOffset(FileStreamPointer, LineBuffer, bufferOffset,
															 BufferSize - (uint)bufferOffset);
			if (_bufferByteCount < 0)
			{
				throw new ApplicationException(string.Format("ERROR: Unable to read data from {0}, _bufferByteCount={1}, check zlib.h or zutil.c for error code meaning.", FilePath,
					_bufferByteCount));
			}
		}

		/// <summary>
		///     Gets the next string in the file.
		/// </summary>
		/// <returns>Returns the next string or null.</returns>
		public string ReadLine()
		{
			string s = "";

			if (_bufferByteCount < 0)
			{
				//throw exception if this is <0
				throw new ApplicationException(string.Format("ERROR: Unable to read data from {0}, _bufferByteCount={1}, check zlib.h or zutil.c for error code meaning.", FilePath,
					_bufferByteCount));
			}
			// skip if the file is not currently open or if we don't have any data in the buffer
			if (!IsOpen || (_bufferByteCount <= 0)) return null;


			int crOffset = -1;
			while (true)
			{

				crOffset = Array.IndexOf(LineBuffer, LineFeedChar, CurrentOffset, _bufferByteCount - CurrentOffset);

				if (crOffset != -1)
				{
					s = s + GetString(crOffset - CurrentOffset);
					CurrentOffset = crOffset + 1;
					break;
				}
				else
				{
					int remainingLen = _bufferByteCount - CurrentOffset;   // remain of this 
					s = s + GetString(remainingLen);

					FillBuffer(0);

					if (_bufferByteCount > 0)
					{
						BufferOffset += CurrentOffset + remainingLen;
						CurrentOffset = 0;
					}
					else if (_bufferByteCount == 0)
					{
						return string.IsNullOrEmpty(s) ? null : s;
					}
					else
					{
						//throw exception if _bufferByteCount <0
						throw new ApplicationException(string.Format("ERROR: Unable to read data from {0}, _bufferByteCount={1}, check zlib.h or zutil.c for error code meaning.", FilePath,
							_bufferByteCount));
					}
				}
			}

			return s;
		}

		/// <summary>
		///     Converts a byte array into a string
		/// </summary>
		/// <param name="len"></param>
		/// <returns></returns>
		private string GetString(int len)
		{
			for (int charIndex = 0; charIndex < len; charIndex++)
			{
				_stringBuffer[charIndex] = (char)LineBuffer[CurrentOffset + charIndex];
			}
			return new string(_stringBuffer, 0, len);
		}

		/// <summary>
		///     Opens the file
		/// </summary>
		public void Open(string filename)
		{
			Open(filename, "rb");
			FillBuffer(0);
		}

		/// <summary>
		///     Moves the file stream pointer to the beginning of the file
		/// </summary>
		public void Rewind()
		{
			SafeNativeMethods.gzrewind(FileStreamPointer);
			CurrentOffset = 0;
			BufferOffset = 0;
			FillBuffer(0);
		}
	}
}
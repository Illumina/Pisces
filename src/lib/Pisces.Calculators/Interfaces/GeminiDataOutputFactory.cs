using System;
using System.IO;
using Gemini.Interfaces;
using Gemini.IO;

namespace Gemini
{
    public class GeminiTextWriter : ITextWriter
    {
        private readonly string _path;
        private TextWriter _textWriter;
        private int _linesWritten = 0;
        
        public GeminiTextWriter(string path)
        {
            _path = path;
            _textWriter = new StreamWriter(path);
        }
        public void WriteLine(string line)
        {
            _textWriter.WriteLine(line);
            _linesWritten++;
        }

        public void Dispose()
        {
            _textWriter?.Close();
            _textWriter?.Dispose();

            // TODO pulled this in because we originally had it in the indel file writer because it was apparently causing problems. Test if the same problem still exists, otherwise this is overkill.
            if (_linesWritten == 0)
            {
                File.CreateText(_path).Close();
            }
        }
    }
    public interface ITextWriter : IDisposable
    {
        void WriteLine(string line);
    }
    
    public class GeminiDataOutputFactory : IGeminiDataOutputFactory
    {
        private readonly int _numThreads;

        public GeminiDataOutputFactory(int numThreads)
        {
            _numThreads = numThreads;
        }
        private IBamWriterFactory GetBamWriterFactory(string inBam)
        {
            var bamWriterFactory = new BamWriterFactory(_numThreads, inBam);
            return bamWriterFactory;
        }


        public ITextWriter GetTextWriter(string outFile)
        {
            return new GeminiTextWriter(outFile);
        }

        public IWriterSource GetWriterSource(string inBam, string outBam)
        {
            var writerFactory = GetBamWriterFactory(inBam);
            var writerSource =
                new WriterSourceRecycled(outBam, writerFactory);
            return writerSource;
        }

    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crc32C;

namespace MapleStory.Sampler
{
    public class TfRecordWriter : IDisposable
    {
        public const string TfRecordNameExtension = ".tfrecord";
        private const int BufferSize = 5120;
        private FileStream _fileStream;

        /// <summary>
        /// Create a TfRecordWriter, by creating an underlying <see cref="FileStream"/>
        /// </summary>
        /// <param name="path">If path does not end with ".tfrecord", it will be added.</param>
        public TfRecordWriter(string path)
        {
            if (!path.EndsWith(TfRecordNameExtension, StringComparison.InvariantCultureIgnoreCase))
            {
                path += TfRecordNameExtension;
            }
            _fileStream = new FileStream(path, FileMode.CreateNew);
        }

        ~TfRecordWriter()
        {
            this.Dispose();
        }

        public void Write(TfExample example)
        {
            Write(example.SerializeToStream());
        }

        public void Write(byte[] bytes)
        {
            Write(new MemoryStream(bytes));
        }

        /// <summary>
        /// Write a single record. 
        /// </summary>
        /// <param name="stream">Stream containing record in binary form</param>
        public void Write(Stream stream)
        {
            ulong length = (ulong)stream.Length;
            uint crcLength = Crc32CAlgorithm.Compute(BitConverter.GetBytes(length));
            uint maskLength = MaskCrc32(crcLength);
            _fileStream.Write(BitConverter.GetBytes(length), 0, 8); // uint64 length
            _fileStream.Write(BitConverter.GetBytes(maskLength), 0, 4); // uint32 masked_crc32_of_length
            stream.Seek(0, SeekOrigin.Begin); // Read from head

            byte[] buffer = new byte[BufferSize];
            int count = 0;
            int readSize = stream.Read(buffer, 0, buffer.Length);
            uint crcData = Crc32CAlgorithm.Compute(buffer, 0, readSize);
            for (bool firstRun = true; readSize > 0;
                count += readSize, readSize = stream.Read(buffer, 0, buffer.Length), firstRun = false)
            {
                if (!firstRun)
                {
                    crcData = Crc32CAlgorithm.Append(crcData, buffer, 0, readSize);
                }
                _fileStream.Write(buffer, 0, readSize); // byte data[length]
            }

            if (count != (int)length)
            {
                throw new Exception("Stream length does not equal to read length.");
            }

            uint maskCrcData = MaskCrc32(crcData);
            _fileStream.Write(BitConverter.GetBytes(maskCrcData), 0, 4); // uint32 masked_crc32_of_data
        }

        /// <summary>
        /// See <seealso cref="https://www.tensorflow.org/tutorials/load_data/tfrecord#tfexample"/>
        /// </summary>
        private uint MaskCrc32(uint crc)
        {
            return (uint)(((crc >> 15) | (crc << 17)) + 0xa282ead8UL);
        }


        public void Dispose()
        {
            _fileStream?.Dispose();
        }
    }
}

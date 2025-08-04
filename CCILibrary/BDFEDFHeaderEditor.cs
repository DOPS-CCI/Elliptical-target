using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BDFEDFFileStream
{
    public class BDFEDFHeaderEditor
    {
        BDFEDFHeader Header;
        StreamWriter fileStream;
        BinaryReader br;
        bool labelChanged = false;
        bool typeChanged = false;
        bool prefilterChanged = false;
        bool physicalDimensionChanged = false;
        bool subjectIDChanged = false;
        bool recordingIDChanged = false;
        bool numberOfRecordsChanged = false;
        long streamLength;

        /// <summary>
        /// Any unwritten edits?
        /// </summary>
        public bool HasChanged
        {
            get { return labelChanged || typeChanged || prefilterChanged ||
                physicalDimensionChanged || subjectIDChanged || recordingIDChanged || numberOfRecordsChanged; }
        }
        public BDFEDFHeaderEditor(Stream stream)
        {
            if (stream.CanRead && stream.CanWrite && stream.CanSeek)
            {
                Header = new BDFEDFHeader();
                BinaryReader br = new BinaryReader(stream, Encoding.ASCII);
                Header.read(br);
                fileStream = new StreamWriter(br.BaseStream, Encoding.ASCII);
                streamLength = stream.Length;
            }
            else
                throw (new Exception("BDFEDFHeaderEditor stream must be read/write/seek"));
        }

        public void ChangeSubjectID(string s)
        {
            if (Header.localSubjectId == s) return;
            Header.localSubjectId = s;
            subjectIDChanged = true;
        }

        public void ChangeRecordingID(string s)
        {
            if (Header.localRecordingId == s) return;
            Header.localRecordingId = s;
            recordingIDChanged = true;
        }

        /// <summary>
        /// Coreect record count to coincide with calculated from file size
        /// </summary>
        public void CorrectNumberOfRecords()
        {
            if (Header.numberOfRecords == RecordCount) return;
            Header.numberOfRecords = RecordCount;
            numberOfRecordsChanged = true;
        }

        public void ChangeChannelLabel(int index, string s)
        {
            if (Header.channelLabels[index] == s) return;
            Header.channelLabels[index] = s;
            labelChanged = true;
        }

        public void ChangeTransducerType(int index, string s)
        {
            if (Header.transducerTypes[index] == s) return;
            Header.transducerTypes[index] = s;
            typeChanged = true;
        }

        public void ChangePrefilter(int index, string s)
        {
            if (Header.channelPrefilters[index] == s) return;
            Header.channelPrefilters[index] = s;
            prefilterChanged = true;
        }

        public void ChangePhysicalDimension(int index, string s)
        {
            if (Header.physicalDimensions[index] == s) return;
            Header.physicalDimensions[index] = s;
            physicalDimensionChanged = true;
        }

        public string SubjectID { get { return Header.localSubjectId; } }
        public string RecordingID { get { return Header.localRecordingId; } }
        public string StartDate { get { return Header.timeOfRecording.ToString("dd.MM.yy"); } }
        public string StartTime { get { return Header.timeOfRecording.ToString("hh.mm.ss"); } }
        public string HeaderLength { get { return Header.headerSize.ToString("0"); } }
        public string NRecords { get { return Header.numberOfRecords.ToString("0"); } }
        public string RecDuration { get { return Header.recordDurationDouble.ToString("0.000"); } }
        public string NChannels { get { return Header.nActualChannels.ToString("0"); } }
        public int RecordSize {
            get {
                int nSamples = 0;
                for (int ch = 0; ch < Header.nActualChannels; ch++) nSamples += Header.numberSamples[ch];
                return nSamples * Header._bytesPerSample;
            }
        }

        /// <summary>
        /// Calculated record count based on file length; should equal Header.numberOfRecords
        /// </summary>
        public int RecordCount { get { return (int)Math.Floor((double)(streamLength - Header.headerSize) / RecordSize); } }

        /// <summary>
        /// Length of header in bytes as recorded in file header record
        /// </summary>
        /// <returns>Recorded header length</returns>
        public int GetHeaderLength()
        {
            return Header.headerSize;
        }

        /// <summary>
        /// Number of records as recorded in file header record
        /// </summary>
        /// <returns>Recorded number of records</returns>
        public int GetNumberOfRecords()
        {
            return Header.numberOfRecords;
        }

        public string[] GetChannelLabels()
        {
            return Header.channelLabels;
        }

        public string[] GetTransducerTypes()
        {
            return Header.transducerTypes;
        }

        public string[] GetPrefilters()
        {
            return Header.channelPrefilters;
        }

        public string[] GetPhysicalDimensions()
        {
            return Header.physicalDimensions;
        }

        public void RewriteHeader()
        {
            if (subjectIDChanged)
            {
                fileStream.BaseStream.Seek(8L, SeekOrigin.Begin);
                fileStream.Write("{0,-80}",
                    Header.localSubjectId.Substring(0, Math.Min(80, Header.localSubjectId.Length)));
                fileStream.Flush();
                subjectIDChanged = false;
            }

            if (recordingIDChanged)
            {
                fileStream.BaseStream.Seek(88L, SeekOrigin.Begin);
                fileStream.Write("{0,-80}",
                    Header.localRecordingId.Substring(0, Math.Min(80, Header.localRecordingId.Length)));
                fileStream.Flush();
                recordingIDChanged = false;
            }

            if (numberOfRecordsChanged)
            {
                fileStream.BaseStream.Seek(236L, SeekOrigin.Begin);
                fileStream.Write("{0,-8:0}", Header.numberOfRecords);
                fileStream.Flush();
                numberOfRecordsChanged = false;
            }

            if (labelChanged)
            {
                fileStream.BaseStream.Seek(256L, SeekOrigin.Begin);
                foreach (string cL in Header.channelLabels)
                    fileStream.Write("{0,-16}", cL.Substring(0, Math.Min(16, cL.Length)));
                fileStream.Flush();
                labelChanged = false;
            }

            if (typeChanged)
            {
                fileStream.BaseStream.Seek(256L + 16 * Header.numberChannels, SeekOrigin.Begin);
                foreach (string tT in Header.transducerTypes)
                    fileStream.Write("{0,-80}", tT.Substring(0, Math.Min(80, tT.Length)));
                fileStream.Flush();
                typeChanged = false;
            }

            if (physicalDimensionChanged)
            {
                fileStream.BaseStream.Seek(256L + 96 * Header.numberChannels, SeekOrigin.Begin);
                foreach (string pD in Header.physicalDimensions)
                    fileStream.Write("{0,-8}", pD.Substring(0, Math.Min(8, pD.Length)));
                fileStream.Flush();
                physicalDimensionChanged = false;
            }

            if (prefilterChanged)
            {
                fileStream.BaseStream.Seek(256L + 136 * Header.numberChannels, SeekOrigin.Begin);
                foreach (string pF in Header.channelPrefilters)
                    fileStream.Write("{0,-80}", pF.Substring(0, Math.Min(80, pF.Length)));
                fileStream.Flush();
                prefilterChanged = false;
            }
        }

        public void Close()
        {
            fileStream.Close();
        }
    }
}

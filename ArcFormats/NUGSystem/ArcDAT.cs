//! \file       ArcDAT.cs
//! \date       Fri Mar 23 03:14:00 2018
//! \brief      NUG System resource archive.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GameRes.Formats.NUGSystem
{
    [Export(typeof(ArchiveFormat))]
    public class DatOpener : ArchiveFormat
    {
        public override string         Tag { get { return "DAT"; } }
        public override string Description { get { return "NUG System resource archive"; } }
        public override uint     Signature { get { return 0; } }
        public override bool  IsHierarchic { get { return true; } }
        public override bool      CanWrite { get { return false; } }
        public DatOpener()
        {
            Extensions = new string[] { "dat" };
            Settings = new[] { DatEncoding };
        }

        EncodingSetting DatEncoding = new EncodingSetting("AWFEncodingCP");


        public override ArcFile TryOpen(ArcView file)
        {
            if (!file.View.AsciiEqual(0, "1AWF"))
                return null;
            return Open1AWF(file);
        }

        ArcFile Open1AWF(ArcView file)
        {
            int count = file.View.ReadInt32(12);
            uint index_size = (uint)count * 64;
            if (!IsSaneCount(count))
                return null;
            //var encoding = DatEncoding.Get<Encoding>();
            var encoding = Encoding.GetEncoding(932);
            // index table is at tail of the file.
            uint index_table_offset = file.View.ReadUInt32(16) + (uint)count * 4;
            var index = file.View.ReadBytes(index_table_offset, index_size);
            var dir = new List<Entry>(count);
            int index_offset = 4;
            for (int i = 0; i < count; ++i)
            {
                var name = encoding.GetString(index, index_offset + 12, 48);
                name = name.TrimEnd(new char[] { '\0' });
                var entry = FormatCatalog.Instance.Create<Entry>(name);
                entry.Offset = index.ToUInt32(index_offset);
                entry.Size = index.ToUInt32(index_offset + 4);
                if (!entry.CheckPlacement(file.MaxOffset))
                    return null;
                index_offset += 64;
                dir.Add(entry);
            }
            return new ArcFile(file, this, dir);
        }
    }
}

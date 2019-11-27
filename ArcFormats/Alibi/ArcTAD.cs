//! \file       ArcTAD.cs
//! \date       2019 Nov 28
//! \brief      Alibi/Alibi+ resource archive.
//
// Copyright (C) 2019 by Y-H Lin
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;

namespace GameRes.Formats.Alibi
{
    [Export(typeof(ArchiveFormat))]
    class TadOpener : ArchiveFormat
    {
        public override string Tag => "TAD";
        public override string Description => "Alibi resource archive";
        public override uint Signature => 0;
        public override bool IsHierarchic => false;

        private uint ReadNextInteger(ArcViewStream stream)
        {
            using (var buffer = new MemoryStream())
            {
                byte b = Byte.MinValue;
                while (true)
                {
                    b = stream.ReadUInt8();
                    if (b == 0x20)
                    {
                        var numStr = (new Utility.AsciiString(buffer.GetBuffer())).ToString();
                        stream.ReadUInt8(); // skip 'q'
                        return uint.Parse(numStr);
                    }
                    buffer.WriteByte(b);
                }
            }
        }

        public override ArcFile TryOpen(ArcView file)
        {
            using (var input = file.CreateStream())
            {
                uint count = ReadNextInteger(input);
                uint[] entrySizeList = new uint[count];

                for (uint i = 0; i < count; ++i)
                {
                    entrySizeList[i] = ReadNextInteger(input);
                }

                var data_offset = input.Position;
                var entries = new Entry[count];

                for (uint i = 0; i < count; ++i)
                {
                    var entry = new Entry();
                    entry.Name = i.ToString("D5");
                    entry.Offset = data_offset;
                    entry.Size = entrySizeList[i];
                    entry.Type = FormatCatalog.Instance.GetTypeFromName(entry.Name + ".png");
                    data_offset += entrySizeList[i];
                    entries[i] = entry;
                }

                return new ArcFile(file, this, entries);
            }
        }
    }
}


using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using GameRes.Utility;

namespace GameRes.Legacy.Softgarage
{
    public class AcxEntry : PackedEntry
    {
        public string DateTime;
    }

#if DEBUG
    [Export(typeof(ArchiveFormat))]
#endif
    public class AcxOpener : ArchiveFormat
    {
        public AcxOpener()
        {
            Extensions = new string[] {"ac1", "ac2", "ac3", "ac4", "ac5"};
        }

        public override string Tag { get { return "ACx"; } }
        public override string Description { get { return "AdvSystem archive for 株式会社ソフトガレージ (softgarge)"; } }
        public override uint Signature { get { return 0; } }
        public override bool IsHierarchic { get { return true; } }
        public override bool CanWrite { get { return false; } }

        public static void DecryptEntry(byte[] raw)
        {
            for (long i = 0; i < raw.LongLength; ++i)
            {
                byte a = raw[i];
                byte b = a;
                b >>= 4;
                b &= 0xf;
                a <<= 4;
                b += a;
                b = (byte) ~b;
                raw[i] = b;
            }
        }

        public override ArcFile TryOpen(ArcView file)
        {
            int count = file.View.ReadInt32(0);
            if (!IsSaneCount(count))
                return null;
            uint index_offset = file.View.ReadUInt32(4);
            if (index_offset != 0x0C)
                return null;

            uint first_offset = file.View.ReadUInt32(8);


            var dir = new List<Entry>(count);
            const int EntrySize = 100;
            byte[] rawEntryBuffer = new byte[EntrySize];

            for (int i = 0; i < count; ++i)
            {
                file.View.Read(index_offset, rawEntryBuffer, 0, EntrySize);
                DecryptEntry(rawEntryBuffer);

                var entry = FormatCatalog.Instance.Create<AcxEntry>(Encodings.cp932.GetString(rawEntryBuffer, 0, 64)
                    .TrimEnd('\0').Trim());
                if (entry.Name.ToUpper().EndsWith("STX"))
                {
                    entry.Type = "script";
                }
                entry.Offset = LittleEndian.ToInt32(rawEntryBuffer, 68) + first_offset;
                entry.UnpackedSize = LittleEndian.ToUInt32(rawEntryBuffer, 76);
                entry.Size = LittleEndian.ToUInt32(rawEntryBuffer, 64);
                entry.IsPacked = LittleEndian.ToInt32(rawEntryBuffer, 72) != 0;
                entry.DateTime = Encodings.cp932.GetString(rawEntryBuffer, 80, 20).TrimEnd('\0').Trim();

                if (!entry.CheckPlacement(file.MaxOffset))
                    return null;
                dir.Add(entry);
                index_offset += EntrySize;
            }
            return new ArcFile(file, this, dir);
        }

        public override Stream OpenEntry(ArcFile arc, Entry entry)
        {
            var input = arc.File.CreateStream(entry.Offset, entry.Size, entry.Name);
            var pent = entry as AcxEntry;
            byte[] tagEntry = null;

            if (null != pent)
            {

                byte[] rawEntry = input.ReadBytes(unchecked((int) pent.Size));
                if (pent.IsPacked)
                {
                    byte[] decodedEntry = new byte[pent.UnpackedSize];
                    Decompress(rawEntry, decodedEntry);
                    tagEntry = decodedEntry;
                }
                else
                {
                    tagEntry = rawEntry;
                }
                return new BinMemoryStream(tagEntry, pent.Name);
            }

            return null;
        }
        /// <summary>
        /// Maybe someone can replace this with human readable one?
        /// </summary>
        private unsafe void Decompress(byte[] src, byte[] dest)
        {
            var MEMBuf = new byte[0x183d24];
            fixed (byte* pMEM = MEMBuf)
            {
                int top_ptr = MEMBuf.Length;
                int heap_ptr = 4;

                int p_src = heap_ptr;
                src.CopyTo(MEMBuf, p_src);
                int p_dest = heap_ptr + src.Length;
                int p_cache = heap_ptr + src.Length + dest.Length;
                heap_ptr += src.Length + dest.Length + 0x10000;

                int eax = 0;
                int ebx = 0;
                int ecx = 0;
                int edx = 0;
                int esp = top_ptr;
                int ebp = 0;
                int esi = 0;
                int edi = 0;

                // Fake call stack prologue
                esp -= 4;
                *(int*)&pMEM[esp] = 0; //pEntryObj
                esp -= 4;
                *(int*)&pMEM[esp] = p_dest;
                esp -= 4;
                *(int*)&pMEM[esp] = src.Length; // size
                esp -= 4;
                *(int*)&pMEM[esp] = p_src;
                esp -= 4;
                *(int*)&pMEM[esp] = 0; // return

                // Fake backup context
                esp -= 4;
                *(int*)&pMEM[esp] = ecx;
                esp -= 4;
                *(int*)&pMEM[esp] = ebp;
                esp -= 4;
                *(int*)&pMEM[esp] = esi;
                esp -= 4;
                *(int*)&pMEM[esp] = edi;

                ecx = 0x404;
                eax = 0;
                edi = p_cache;
                while (ecx > 0)
                {
                    ecx--;
                    *(int*)&pMEM[edi] = eax;
                    edi += 4;
                }
                pMEM[edi++] = 0;
                eax = *(int*)&pMEM[esp + 0x18];
                ebp = 0;
                edx = 0;
                *(int*)&pMEM[esp + 0xC] = ebp;
                esi = 0xfee;
                if (eax > 0)
                {
                    esp -= 4;
                    *(int*)&pMEM[esp] = ebx;

                    ebx = *(int*)&pMEM[esp + 0x18];

                    do
                    {
                        //shr word [esp+0x10], 1
                        *(ushort*)&pMEM[esp + 0x10] >>= 1;
                        eax = *(int*)&pMEM[esp + 0x10];

                        if ((eax & 0x100) == 0)
                        {
                            *(ushort*)&eax = (ushort)pMEM[ebx + edx];
                            edx++;
                            eax |= 0xff00;
                            *(int*)&pMEM[esp + 0x10] = eax;
                        }
                        eax = *(int*)&pMEM[esp + 0x10];
                        if ((pMEM[esp + 0x10] & 1) != 0)
                        {
                            *(short*)&eax = pMEM[edx + ebx];
                            ecx = *(int*)&pMEM[esp + 0x20];

                            edx++;
                            pMEM[ecx + ebp] = (byte)eax;
                            ebp++;
                            pMEM[p_cache + esi] = (byte)eax;
                            esi++;
                            esi &= 0xfff;
                        }
                        else
                        {
                            *(ushort*)&edi = pMEM[edx + ebx];
                            *(ushort*)&eax = pMEM[edx + ebx + 1];
                            edx++;
                            *(byte*)&ecx = (byte)eax;
                            eax &= 0xf;
                            ecx &= 0xf0;
                            eax += 2;
                            edx++;
                            eax = (int)(short)eax;
                            ecx <<= 4;
                            edi |= ecx;
                            if (eax >= 0)
                            {
                                eax++;
                                ecx = (int)(short)edi;
                                *(int*)&pMEM[esp + 0x18] = eax;
                                do
                                {
                                    edi = *(int*)&pMEM[esp + 0x20];
                                    eax = ecx;
                                    eax &= 0xfff;
                                    ebp++;
                                    esi++;
                                    *(short*)&eax = pMEM[p_cache + eax];
                                    pMEM[edi + ebp - 1] = (byte)eax;
                                    pMEM[esi + p_cache - 1] = (byte)eax;
                                    eax = *(int*)&pMEM[esp + 0x18];
                                    esi &= 0xfff;
                                    ecx++;
                                    eax--;
                                    *(int*)&pMEM[esp + 0x18] = eax;
                                } while (eax != 0);
                            }
                        }
                    } while (edx < *(int*)&pMEM[esp + 0x1C]);
                }

                Array.Copy(MEMBuf, p_dest, dest, 0, dest.Length);
            }
        }
    }
}

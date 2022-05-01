/* Copyright (C) 2021 Chosen Few Software
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using QuesoStruct;
using QuesoStruct.Types.Collections;
using QuesoStruct.Types.Pointers;

namespace DATOneArchiver
{
    [StructType]
    public partial class FileTable : IPointerOwner, ICollectionOwner<Blob>, ICollectionOwner<Entry>
    {
        public IStructInstance RelativeOffsetBase => Parent;
        public long AddedOffsetFromBase => 0;

        public bool IsNullPointer(IStructReference refr) => false;
        public void SetNullPointer(IStructReference refr) { }

        public bool TerminateOnStreamEnd => false;
        public bool IsTerminator(IStructInstance inst) => false;

        [StructMember]
        public uint Checksum { get; set; }

        [StructMember]
        public uint NumBlobs { get; set; }
        long? ICollectionOwner<Blob>.ItemCount => NumBlobs;

        [StructMember]
        public Collection<Blob> Blobs { get; set; }

        [StructMember]
        public uint NumEntries { get; set; }
        long? ICollectionOwner<Entry>.ItemCount => NumEntries;

        [StructMember]
        public Collection<Entry> Entries { get; set; }

        [StructMember]
        public NameList Names { get; set; }
    }
}

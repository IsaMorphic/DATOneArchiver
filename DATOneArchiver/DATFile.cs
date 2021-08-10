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
using QuesoStruct.Types.Pointers;
using QuesoStruct.Types.Primitives;

namespace DATOneArchiver
{
    [StructType]
    public partial class DATFile
    {
        [StructType]
        public partial class TablePtr : IPointerOwner
        {
            public IStructInstance RelativeOffsetBase => this;
            public long AddedOffsetFromBase => 0;

            public bool IsNullPointer(IStructReference refr) => false;
            public void SetNullPointer(IStructReference refr) { }

            [StructMember]
            [AutoInitialize]
            public UInt32Pointer<FileTable> Pointer { get; set; }
        }

        [StructType]
        public partial class EndPtr : IPointerOwner
        {
            public IStructInstance RelativeOffsetBase => (Parent as DATFile).Table.Pointer.Instance;
            public long AddedOffsetFromBase => 0;

            public bool IsNullPointer(IStructReference refr) => false;
            public void SetNullPointer(IStructReference refr) { }

            [StructMember]
            [AutoInitialize]
            public UInt32Pointer<NullTerminatingString> Pointer { get; set; }
        }

        [StructMember]
        [AutoInitialize]
        public TablePtr Table { get; set; }

        [StructMember]
        [AutoInitialize]
        public EndPtr End { get; set; }
    }
}

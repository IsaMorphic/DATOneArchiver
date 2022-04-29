/* Copyright (C) 2022 Chosen Few Software
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

using QuesoStruct.Types.Collections;
using QuesoStruct.Types.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DATOneArchiver
{
    public partial class Archive
    {
        public class Node : IComparable<Node>
        {
            public string Name { get; }
            public Dictionary<string, Node> Children { get; }

            public short? BlobIndex { get; set; }

            public Stream Stream { get; set; }

            public Node this[string path, bool dir = false]
            {
                get
                {
                    var tokens = Path.TrimEndingDirectorySeparator(path)
                        .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

                    var current = this;
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        var token = tokens[i];

                        if (current.Children.ContainsKey(token))
                            current = current.Children[token];
                        else
                        {
                            Node node;
                            if (!dir && i == tokens.Length - 1)
                                node = new Node(token, null);
                            else
                                node = new Node(token);

                            current.Children.Add(token, node);
                            current = node;
                        }
                    }

                    return current;
                }
            }

            public Node()
            {
                Children = new Dictionary<string, Node>();
            }

            public Node(string name)
            {
                Name = name;
                Children = new Dictionary<string, Node>();
            }

            public Node(string name, short? blobIndex)
            {
                Name = name;
                BlobIndex = blobIndex;
            }

            public void PrintNodes(int level = 1, bool isLast = true, HashSet<int> lastLevels = null)
            {
                if (lastLevels == null)
                    lastLevels = new HashSet<int>();
                else
                    lastLevels = new HashSet<int>(lastLevels);

                var indent = new char[level * 3];
                Array.Fill(indent, ' ');

                for (int i = 1; i < level; i++)
                {
                    if (!lastLevels.Contains(i))
                        indent[i * 3] = '\u2502';
                }

                int idx = (level - 1) * 3;
                indent[idx + 0] = isLast ? '\u2514' : '\u251C';
                indent[idx + 1] = '\u2500';
                indent[idx + 2] = '\u2500';

                Logger.WriteLine(new string(indent) + Name);

                if (Children != null)
                {
                    if (isLast) lastLevels.Add(level - 1);

                    int count = 0;
                    foreach (var child in Children.Values)
                    {
                        child.PrintNodes(level + 1, ++count == Children.Count, lastLevels);
                    }
                }
            }

            public int WalkNodes(Game game, SortedList<int, Node> nodes, Collection<Entry> entries, Collection<NullTerminatingString> strings, int startIdx, ref int blobIdx, out int firstIdx)
            {
                firstIdx = 0;
                int idx = startIdx;
                bool isFirstChild = true;

                foreach (var child in Children.Values.OrderBy(c => c))
                {
                    var entry = new Entry(entries);
                    entries.Add(entry);

                    var str = new NullTerminatingString(entry) { Value = child.Name };
                    strings.Add(str);

                    if (child.Children == null)
                    {
                        child.BlobIndex = entry.BlobIndex = (short)blobIdx--;
                        nodes.Add(-child.BlobIndex.Value, child);

                        if (!isFirstChild)
                            entry.NodeIndex = (short)idx++;
                        else
                        {
                            firstIdx = ++idx;
                            isFirstChild = false;
                        }
                    }
                    else
                    {
                        var newIdx = child.WalkNodes(game, nodes, entries, strings, idx + 1, ref blobIdx, out int tempFirst);

                        if (child.Children.Values.All(c => c.Children == null))
                            entry.BlobIndex = (short)newIdx;
                        else
                            entry.BlobIndex = (short)tempFirst;

                        if (!isFirstChild)
                        {
                            if ((game == Game.LSW1 && BUZZ_WORDS.Contains(child.Name.ToLowerInvariant())) ||
                                (game == Game.LSW2 && Children.Values.OrderBy(c => c).First(c => c.Children != null) == child))
                                entry.NodeIndex = (short)idx;
                            else
                                entry.NodeIndex = (short)firstIdx;
                        }
                        else isFirstChild = false;

                        firstIdx = idx + 1;
                        idx = newIdx;
                    }
                }

                return idx;
            }

            public int CompareTo(Node other)
            {
                if ((Children == null && other.Children == null) ||
                    (Children != null && other.Children != null) ||
                    (BUZZ_WORDS.Contains(Name) && BUZZ_WORDS.Contains(other.Name)))
                {
                    var filePattern = new Regex("^(.*?)\\.(.*)");

                    var myMatch = filePattern.Match(Name);

                    string myName, myExt, otherName, otherExt;

                    if (myMatch.Success)
                    {
                        myName = myMatch.Groups[1].Value;
                        myExt = myMatch.Groups[2].Value;
                    }
                    else
                    {
                        myName = Name;
                        myExt = "";
                    }

                    var otherMatch = filePattern.Match(other.Name);

                    if (otherMatch.Success)
                    {
                        otherName = otherMatch.Groups[1].Value;
                        otherExt = otherMatch.Groups[2].Value;
                    }
                    else
                    {
                        otherName = other.Name;
                        otherExt = "";
                    }

                    if (BUZZ_WORDS.Any(w => myName.ToLowerInvariant().EndsWith("-" + w)))
                    {
                        return -1;
                    }
                    else if (BUZZ_WORDS.Any(w => otherName.ToLowerInvariant().EndsWith("-" + w)))
                    {
                        return 1;
                    }
                    else
                    {
                        var nameResult = myName.Replace('_', 'z')
                            .CompareTo(otherName.Replace('_', 'z'));

                        var extResult = myExt.Replace('_', 'z')
                            .CompareTo(otherExt.Replace('_', 'z'));

                        return nameResult != 0 ? nameResult : extResult;
                    }
                }
                else if (Children == null || BUZZ_WORDS.Contains(other.Name))
                {
                    return -1;
                }
                else if (other.Children == null || BUZZ_WORDS.Contains(Name))
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }
    }
}

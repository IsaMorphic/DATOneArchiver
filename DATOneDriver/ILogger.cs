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

using System;

namespace DATOneArchiver
{
    public interface ILogger
    {
        void WriteLine(string s);
        void Write(string s);
    }

    public class ConsoleLogger : ILogger
    {
        public void WriteLine(string s) => Console.WriteLine(s);
        public void Write(string s) => Console.Write(s);
    }
}

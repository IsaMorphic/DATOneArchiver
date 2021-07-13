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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CustomActivity
{
    public class Logger
    {
        public static void WriteLine(string message)
        {
            string timeStamp = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss");
            string logLine = timeStamp + " | " + message;

            Console.WriteLine(logLine);
        }
    }
}
